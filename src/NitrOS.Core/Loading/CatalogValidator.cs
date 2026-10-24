using System.Globalization;
using System.Text.RegularExpressions;
using NitrOS.Core.Model;

namespace NitrOS.Core.Loading;

/// <summary>Rejects broken switch definitions before they can reach the tuner or ISO builder.</summary>
public static partial class CatalogValidator
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KebabCase();

    [GeneratedRegex("^([0-9a-fA-F]{2})*$")]
    private static partial Regex HexBytes();

    public static IReadOnlyList<CatalogError> Validate(IReadOnlyList<(string File, GroupFile Group)> files)
    {
        var errors = new List<CatalogError>();
        var allIds = new HashSet<string>(files.SelectMany(f => f.Group.Switches).Select(s => s.Id), StringComparer.Ordinal);

        foreach (var (file, group) in files)
        {
            CheckGroupName(file, group, errors);
            foreach (var sw in group.Switches)
            {
                CheckId(file, sw, errors);
                CheckEnums(file, sw, errors);
                CheckRequiredText(file, sw, errors);
                CheckDescription(file, sw, errors);
                CheckRequires(file, sw, allIds, errors);
                CheckBreaks(file, sw, errors);
                CheckModes(file, sw, errors);
                CheckPackageRemove(file, sw, errors);
                CheckReversible(file, sw, errors);
                CheckRegistry(file, sw, errors);
                CheckPower(file, sw, errors);
                CheckLiveOnlyActions(file, sw, errors);
                CheckStartupDisable(file, sw, errors);
                CheckRegistryDelete(file, sw, errors);
                CheckPowerScheme(file, sw, errors);
            }
        }

        CheckDuplicates(files, errors);
        return errors;
    }

    private static void Add(List<CatalogError> errors, string file, SwitchDef? sw, string message) =>
        errors.Add(new CatalogError(file, null, sw?.Id, message));

    private static void CheckGroupName(string file, GroupFile group, List<CatalogError> errors)
    {
        var expected = file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? file[..^5] : file;
        if (!string.Equals(group.Group, expected, StringComparison.Ordinal))
            Add(errors, file, null, $"Group '{group.Group}' does not match file name '{file}'.");
    }

    private static void CheckId(string file, SwitchDef sw, List<CatalogError> errors)
    {
        if (!KebabCase().IsMatch(sw.Id))
            Add(errors, file, sw, $"Switch id '{sw.Id}' must be kebab-case (lowercase letters, digits, single dashes).");
    }

    private static void CheckRequiredText(string file, SwitchDef sw, List<CatalogError> errors)
    {
        void Need(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
                Add(errors, file, sw, $"The {label} must not be empty.");
        }

        Need(sw.Name, "name");
        Need(sw.Feature, "feature");
        foreach (var b in sw.Breaks) Need(b, "breaks sentence");
        if (sw.Modes.Count == 0)
            Add(errors, file, sw, "A switch needs at least one mode.");
        if (sw.Measured is { } m)
            Need(m.WindowsBuild, "measured windowsBuild");

        foreach (var a in sw.Live.Concat(sw.Image))
        {
            switch (a)
            {
                case RegistrySet r: Need(r.Key, "registry key"); break; // an empty value name is the key's default value
                case ServiceStart s: Need(s.Service, "service.start service"); break;
                case TaskDisable t: Need(t.Path, "task.disable path"); break;
                case AppxRemove x: Need(x.Name, "appx.remove name"); break;
                case CapabilityRemove c: Need(c.Name, "capability.remove name"); break;
                case FeatureDisable f: Need(f.Name, "feature.disable name"); break;
                case PackageRemove p: Need(p.Pattern, "package.remove pattern"); break;
                case FileRemove fr: Need(fr.Path, "file.remove path"); break;
                case Script sc: Need(sc.Body, "script body"); break;
                case ProgramUninstall pu:
                    Need(pu.DisplayName, "program.uninstall displayName");
                    Need(pu.Publisher, "program.uninstall publisher");
                    break;
                case StartupDisable sd: Need(sd.Name, "startup.disable name"); break;
                case RegistryDelete rd: Need(rd.Key, "registry.delete key"); break;
                case LaunchAtLogon la: Need(la.Program, "launch.at-logon program"); break;
            }
        }
    }

    private static void CheckEnums(string file, SwitchDef sw, List<CatalogError> errors)
    {
        if (!Enum.IsDefined(sw.Risk))
            Add(errors, file, sw, $"Risk value {(int)sw.Risk} is not valid.");
        foreach (var m in sw.Modes.Where(m => !Enum.IsDefined(m)))
            Add(errors, file, sw, $"Mode value {(int)m} is not valid.");
        foreach (var t in sw.SkipOn.Where(t => !Enum.IsDefined(t)))
            Add(errors, file, sw, $"skipOn value {(int)t} is not valid.");
        foreach (var (card, value) in new[]
                 {
                     ("recommended", sw.Cards.Recommended), ("gamer", sw.Cards.Gamer),
                     ("streamer", sw.Cards.Streamer), ("maxLean", sw.Cards.MaxLean),
                 })
        {
            if (!Enum.IsDefined(value))
                Add(errors, file, sw, $"Card default {(int)value} for '{card}' is not valid.");
        }
        foreach (var s in sw.Live.Concat(sw.Image).OfType<ServiceStart>().Where(s => !Enum.IsDefined(s.Start)))
            Add(errors, file, sw, $"Service start type {(int)s.Start} is not valid.");
        foreach (var s in sw.Live.Concat(sw.Image).OfType<StartupDisable>().Where(s => !Enum.IsDefined(s.Source)))
            Add(errors, file, sw, $"Startup source {(int)s.Source} is not valid.");
    }

    private static void CheckDuplicates(IReadOnlyList<(string File, GroupFile Group)> files, List<CatalogError> errors)
    {
        var byId = files
            .SelectMany(f => f.Group.Switches.Select(s => (f.File, s.Id)))
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1);
        foreach (var g in byId)
        {
            var names = string.Join(", ", g.Select(x => x.File).Distinct(StringComparer.Ordinal));
            errors.Add(new CatalogError(g.First().File, null, g.Key, $"Duplicate switch id '{g.Key}' in: {names}."));
        }
    }

    private static void CheckDescription(string file, SwitchDef sw, List<CatalogError> errors)
    {
        var d = sw.Description;
        if (string.IsNullOrWhiteSpace(d))
            Add(errors, file, sw, "The description must not be empty.");
        else if (d.Contains('\n') || d.Contains('\r'))
            Add(errors, file, sw, "The description must be a single line.");
        else if (d.Length > 120)
            Add(errors, file, sw, $"The description is {d.Length} characters; the maximum is 120.");
    }

    private static void CheckRequires(string file, SwitchDef sw, HashSet<string> allIds, List<CatalogError> errors)
    {
        foreach (var r in sw.Requires)
        {
            if (r == sw.Id) Add(errors, file, sw, $"Switch '{sw.Id}' requires itself.");
            else if (!allIds.Contains(r)) Add(errors, file, sw, $"Required switch '{r}' does not exist.");
        }
    }

    private static void CheckBreaks(string file, SwitchDef sw, List<CatalogError> errors)
    {
        if (sw.Risk == Risk.Breaks && sw.Breaks.Count == 0)
            Add(errors, file, sw, "Risk 'breaks' needs a non-empty breaks list.");
    }

    private static void CheckModes(string file, SwitchDef sw, List<CatalogError> errors)
    {
        CheckMode(file, sw, Mode.Live, "live", sw.Live, errors);
        CheckMode(file, sw, Mode.Image, "image", sw.Image, errors);
    }

    private static void CheckMode(string file, SwitchDef sw, Mode mode, string name,
        IReadOnlyList<SwitchAction> actions, List<CatalogError> errors)
    {
        var listed = sw.Modes.Contains(mode);
        if (listed && actions.Count == 0)
            Add(errors, file, sw, $"Mode '{name}' is listed but the {name} action list is empty.");
        else if (!listed && actions.Count > 0)
            Add(errors, file, sw, $"The {name} action list has actions but mode '{name}' is not listed.");
    }

    private static void CheckPackageRemove(string file, SwitchDef sw, List<CatalogError> errors)
    {
        if (sw.Live.Any(a => a is PackageRemove))
            Add(errors, file, sw, "package.remove is image-only and must not appear in live.");
    }

    private static void CheckReversible(string file, SwitchDef sw, List<CatalogError> errors)
    {
        if (!sw.Reversible) return;
        if (sw.Live.Any(a => a.IsDestructive))
            Add(errors, file, sw, "A reversible switch cannot have a destructive live action.");
        if (sw.Live.Any(a => a is Script { UndoBody: null or "" }))
            Add(errors, file, sw, "A reversible switch needs undoBody on every live script.");
    }

    private static void CheckRegistry(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Live.Concat(sw.Image).OfType<RegistrySet>())
        {
            if (a.Hive is not ("HKLM" or "HKCU"))
                Add(errors, file, sw, $"Registry hive '{a.Hive}' must be HKLM or HKCU.");
            if (!Enum.IsDefined(a.Kind))
            {
                Add(errors, file, sw, $"Registry kind {(int)a.Kind} is not valid.");
                continue;
            }
            var ok = a.Kind switch
            {
                RegistryKind.Dword => TryNumber(a.Data, 32),
                RegistryKind.Qword => TryNumber(a.Data, 64),
                RegistryKind.Binary => HexBytes().IsMatch(a.Data),
                RegistryKind.String or RegistryKind.ExpandString or RegistryKind.MultiString => true,
                _ => false,
            };
            if (!ok)
                Add(errors, file, sw, $"Registry data '{a.Data}' is not valid for kind {a.Kind}.");
        }
    }

    private static bool TryNumber(string s, int bits)
    {
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return ulong.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var h)
                   && (bits == 64 || h <= uint.MaxValue);
        return ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
               && (bits == 64 || d <= uint.MaxValue);
    }

    private static void CheckPower(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Live.Concat(sw.Image).OfType<PowerSet>())
        {
            if (!Guid.TryParse(a.Subgroup, out _))
                Add(errors, file, sw, $"power.set subgroup '{a.Subgroup}' is not a GUID.");
            if (!Guid.TryParse(a.Setting, out _))
                Add(errors, file, sw, $"power.set setting '{a.Setting}' is not a GUID.");
            if (a.Ac is null && a.Dc is null)
                Add(errors, file, sw, "power.set needs at least one of ac or dc.");
        }
    }

    private static void CheckLiveOnlyActions(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Image)
        {
            var name = a switch
            {
                ProgramUninstall => "program.uninstall",
                PowerScheme => "power.scheme",
                LaunchAtLogon => "launch.at-logon",
                _ => null,
            };
            if (name is not null)
                Add(errors, file, sw, $"{name} is live-only and must not appear in image.");
        }
    }

    private static void CheckStartupDisable(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Live.Concat(sw.Image).OfType<StartupDisable>())
        {
            if (!Enum.IsDefined(a.Source)) continue; // reported by CheckEnums
            if (a.Source == StartupSource.Run)
            {
                if (a.Hive is not ("HKLM" or "HKCU"))
                    Add(errors, file, sw, "startup.disable with source 'run' needs hive HKLM or HKCU.");
            }
            else if (a.Hive is not null)
                Add(errors, file, sw, $"startup.disable with source '{a.Source}' must not set hive.");
        }
        foreach (var a in sw.Image.OfType<StartupDisable>().Where(a => Enum.IsDefined(a.Source) && a.Source != StartupSource.Run))
            Add(errors, file, sw, "startup.disable in image supports only source 'run'.");
    }

    private static void CheckRegistryDelete(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Live.Concat(sw.Image).OfType<RegistryDelete>())
        {
            if (a.Hive is not ("HKLM" or "HKCU"))
                Add(errors, file, sw, $"registry.delete hive '{a.Hive}' must be HKLM or HKCU.");
        }
    }

    private static void CheckPowerScheme(string file, SwitchDef sw, List<CatalogError> errors)
    {
        foreach (var a in sw.Live.Concat(sw.Image).OfType<PowerScheme>())
        {
            if (!Guid.TryParse(a.Scheme, out _))
                Add(errors, file, sw, $"power.scheme '{a.Scheme}' is not a GUID.");
        }
    }
}
