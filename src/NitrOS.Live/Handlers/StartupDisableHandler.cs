using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

/// <summary>
/// Turns off start-up entries. Every match of the (possibly starred) name is changed and its old value saved.
/// A saved entry's Location is "kind|hive|key|name" with kind approved, appx or task; Data is the old value, or
/// empty when there was none. Registry formats were observed on the owner's PC on 2026-10-25.
/// </summary>
public sealed class StartupDisableHandler : IActionHandler
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ApprovedRoot = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\";
    private const string AppxRoot = "Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\AppModel\\SystemAppData";
    private const string NewDisabled = "030000000000000000000000";

    private sealed record Target(string Kind, LiveHive Hive, string Key, string Name, string Old);

    private static string Loc(Target t) => $"{t.Kind}|{HiveMap.Name(t.Hive)}|{t.Key}|{t.Name}";

    private static bool Wild(string n) => n.Contains('*');

    private static bool IsFirstByte(string hex, string b) =>
        hex.Length >= 2 && string.Equals(hex[..2], b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The approved entry as it is now, or null when it must not be touched (first byte other than 02 or 03).</summary>
    private static Target? Approved(LiveSystems s, LiveHive hive, string sub, string name)
    {
        var key = ApprovedRoot + sub;
        var cur = s.Registry.GetValue(hive, key, name, RegView.Default);
        if (cur is null) return new Target("approved", hive, key, name, "");
        if (cur.Kind != RegistryKind.Binary) return null;
        return IsFirstByte(cur.Data, "02") || IsFirstByte(cur.Data, "03") ? new Target("approved", hive, key, name, cur.Data) : null;
    }

    private static List<Target> Resolve(StartupDisable a, LiveSystems s)
    {
        var result = new List<Target>();
        switch (a.Source)
        {
            case StartupSource.Run:
            {
                if (a.Hive is null) throw new ArgumentException("startup.disable of a Run entry needs a hive");
                var hive = HiveMap.Parse(a.Hive);
                var views = hive == LiveHive.LocalMachine ? new[] { RegView.Default, RegView.Registry32 } : new[] { RegView.Default };
                foreach (var view in views)
                    foreach (var n in s.Registry.GetValueNames(hive, RunKey, view).Where(n => Glob.IsMatch(a.Name, n)))
                        if (Approved(s, hive, view == RegView.Registry32 ? "Run32" : "Run", n) is { } t) result.Add(t);
                break;
            }
            case StartupSource.StartupFolder:
            {
                var folders = new (LiveHive Hive, string Path)[]
                {
                    (LiveHive.CurrentUser, "%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\Startup"),
                    (LiveHive.LocalMachine, "%ProgramData%\\Microsoft\\Windows\\Start Menu\\Programs\\StartUp"),
                };
                foreach (var (hive, path) in folders)
                {
                    var names = new HashSet<string>(
                        s.Registry.GetValueNames(hive, ApprovedRoot + "StartupFolder", RegView.Default).Where(n => Glob.IsMatch(a.Name, n)),
                        StringComparer.OrdinalIgnoreCase);
                    if (!Wild(a.Name) && s.Files.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(path), a.Name)))
                        names.Add(a.Name);
                    foreach (var n in names)
                        if (Approved(s, hive, "StartupFolder", n) is { } t) result.Add(t);
                }
                break;
            }
            case StartupSource.AppxTask:
            {
                var bang = a.Name.LastIndexOf('!');
                if (bang < 0) throw new ArgumentException("An appx-task name must look like <PackageFamilyName>!<TaskId>");
                if (!Wild(a.Name))
                {
                    AddAppx(s, result, a.Name[..bang], a.Name[(bang + 1)..]);
                    break;
                }
                var root = s.Registry.ExportKey(LiveHive.CurrentUser, AppxRoot, RegView.Default);
                if (root is null) break;
                foreach (var pkg in root.SubKeys)
                {
                    var pkgName = pkg.Key[(pkg.Key.LastIndexOf('\\') + 1)..];
                    foreach (var task in pkg.SubKeys)
                    {
                        var taskId = task.Key[(task.Key.LastIndexOf('\\') + 1)..];
                        if (Glob.IsMatch(a.Name, pkgName + "!" + taskId)) AddAppx(s, result, pkgName, taskId);
                    }
                }
                break;
            }
            case StartupSource.Task:
                if (s.Tasks.Get(a.Name) is { } info)
                    result.Add(new Target("task", LiveHive.CurrentUser, "", a.Name, info.Enabled ? "true" : "false"));
                break;
        }
        return result;
    }

    private static void AddAppx(LiveSystems s, List<Target> result, string pkg, string taskId)
    {
        var key = AppxRoot + "\\" + pkg + "\\" + taskId;
        var cur = s.Registry.GetValue(LiveHive.CurrentUser, key, "State", RegView.Default);
        if (cur is not null) result.Add(new Target("appx", LiveHive.CurrentUser, key, "State", cur.Data));
    }

    public PriorState Read(SwitchAction action, LiveSystems s)
    {
        var targets = Resolve((StartupDisable)action, s);
        return targets.Count == 0
            ? new MissingPrior()
            : new StartupPrior(targets.Select(t => new StartupEntry(Loc(t), t.Old)).ToList());
    }

    public ApplyResult Apply(SwitchAction action, LiveSystems s)
    {
        var targets = Resolve((StartupDisable)action, s);
        if (targets.Count == 0) return ApplyResult.Skipped("no matching start-up entry");
        foreach (var t in targets)
        {
            switch (t.Kind)
            {
                case "approved":
                    var data = t.Old.Length == 0 ? NewDisabled : "03" + t.Old[2..];
                    s.Registry.SetValue(t.Hive, t.Key, t.Name, new RegValue(RegistryKind.Binary, data), RegView.Default);
                    break;
                case "appx":
                    s.Registry.SetValue(t.Hive, t.Key, t.Name, new RegValue(RegistryKind.Dword, "1"), RegView.Default);
                    break;
                case "task":
                    s.Tasks.SetEnabled(t.Name, false);
                    break;
            }
        }
        return ApplyResult.Applied;
    }

    public void Undo(SwitchAction action, PriorState prior, LiveSystems s)
    {
        if (prior is not StartupPrior p) return;
        foreach (var e in p.Entries)
        {
            var parts = e.Location.Split('|', 4);
            var (kind, hive, key, name) = (parts[0], parts[1], parts[2], parts[3]);
            if (kind == "task")
            {
                var enabled = bool.Parse(e.Data);
                if (s.Tasks.Get(name) is { } now && now.Enabled != enabled) s.Tasks.SetEnabled(name, enabled);
                continue;
            }
            // Only entries that differ from their prior are written back.
            var h = HiveMap.Parse(hive);
            var cur = s.Registry.GetValue(h, key, name, RegView.Default);
            if (e.Data.Length == 0)
            {
                if (cur is not null) s.Registry.DeleteValue(h, key, name, RegView.Default);
                continue;
            }
            var old = new RegValue(kind == "appx" ? RegistryKind.Dword : RegistryKind.Binary, e.Data);
            if (cur != old) s.Registry.SetValue(h, key, name, old, RegView.Default);
        }
    }

    /// <summary>
    /// The HKLM keys this action may write, for the engine's pre-flight: the StartupApproved Run and Run32 keys for an HKLM Run
    /// entry, the StartupApproved StartupFolder key for the all-users Startup folder. Other sources write only HKCU or tasks.
    /// </summary>
    internal static IReadOnlyList<string> HklmKeysWritten(StartupDisable a) => a.Source switch
    {
        StartupSource.Run when a.Hive is { } h && HiveMap.Parse(h) == LiveHive.LocalMachine => [ApprovedRoot + "Run", ApprovedRoot + "Run32"],
        StartupSource.StartupFolder => [ApprovedRoot + "StartupFolder"],
        _ => [],
    };

    public bool IsInEffect(SwitchAction action, LiveSystems s)
    {
        var a = (StartupDisable)action;
        // Targets are re-resolved from the live state; an approved entry that does not exist yet is not in effect.
        var targets = Resolve(a, s).Where(t => t.Old.Length > 0).ToList();
        if (targets.Count == 0) return false;
        return targets.All(t => t.Kind switch
        {
            "approved" => IsFirstByte(t.Old, "03"),
            "appx" => t.Old == "1",
            _ => t.Old == "false",
        });
    }
}
