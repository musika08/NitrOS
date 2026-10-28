using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Iso;
using NitrOS.Image.Systems;
using NitrOS.Image.Windows;
using NitrOS.Live;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Cli;

/// <summary>
/// The testable entry point of the nitros command-line runner. Exit codes: 0 OK, 1 refused or partly failed, 2 bad arguments
/// (including a journal that belongs to another account, for undo and drift).
/// plan, detect, health and journals never call <see cref="CliEnvironment.Systems"/>; apply and undo refuse without --yes and run
/// the session guard before they do.
/// </summary>
public static class CliApp
{
    public const int Ok = 0, Refused = 1, BadArguments = 2;

    private sealed class UsageException(string message) : Exception(message);

    private sealed record Parsed(
        string? Card,
        string[]? Ids,
        bool Yes,
        bool AllowNoRestore,
        string? Catalog,
        string? Source,
        string? Out,
        int? Edition,
        string? ToolsDir,
        bool Discard,
        string? StopAfter,
        string? FirstBootRoute,
        string? IsoCard,
        List<string> Positional);

    public static int Run(string[] args, TextWriter output, LiveSystems systems, CliEnvironment env, CancellationToken ct = default) =>
        Run(args, output, env with { Systems = () => systems }, ct);

    /// <summary><paramref name="ct"/> stops an apply after the current action; that switch is rolled back.</summary>
    public static int Run(string[] args, TextWriter output, CliEnvironment env, CancellationToken ct = default)
    {
        try
        {
            if (args.Length == 0) throw new UsageException("No command given.");
            var command = args[0];
            var p = Parse(args.Skip(1).ToArray());
            return command switch
            {
                "detect" => Detect(p, output, env),
                "plan" => Plan(p, output, env),
                "apply" => Apply(p, output, env, ct),
                "undo" => Undo(p, output, env),
                "drift" => Drift(p, output, env),
                "health" => Health(p, output, env),
                "journals" => Journals(p, output, env),
                "restore-xbox" => RestoreXbox(p, output, env, ct),
                "image-info" => ImageInfo(p, output, env),
                "build-iso" => BuildIso(p, output, env, ct),
                _ => throw new UsageException($"Unknown command '{command}'."),
            };
        }
        catch (UsageException e)
        {
            output.WriteLine($"Error: {e.Message}");
            output.WriteLine(Usage);
            return BadArguments;
        }
        catch (JournalOwnerException e)
        {
            output.WriteLine($"Refused: {e.Message}");
            return BadArguments;
        }
        catch (Exception e)
        {
            output.WriteLine($"Error: {e.Message}");
            return Refused;
        }
    }

    private const string Usage = """
        Usage:
          nitros detect
          nitros plan  --card <recommended|gamer|streamer|max-lean> [--ids a,b] [--catalog <dir>]
          nitros apply --card <...> [--ids a,b] --yes [--allow-without-restore-point] [--catalog <dir>]
          nitros undo  <journal> [--ids a,b] --yes
          nitros drift <journal>
          nitros health [--iso-card <card>]
          nitros journals
          nitros restore-xbox --yes [--catalog <dir>]
          nitros image-info --source <iso|folder>
          nitros build-iso --source <iso|folder> --out <iso> (--card <...> | --ids a,b) [--edition <n>] [--tools-dir <dir>] --yes
          nitros build-iso --out <iso> --discard --yes
        """;

    private static Parsed Parse(string[] args)
    {
        string? card = null, catalog = null, source = null, outIso = null, toolsDir = null, stopAfter = null, firstBootRoute = null, isoCard = null;
        int? edition = null;
        string[]? ids = null;
        bool yes = false, allow = false, discard = false;
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--yes": yes = true; break;
                case "--allow-without-restore-point": allow = true; break;
                case "--card": card = Value(args, ref i); break;
                case "--catalog": catalog = Value(args, ref i); break;
                case "--source": source = Value(args, ref i); break;
                case "--out": outIso = Value(args, ref i); break;
                case "--edition":
                    if (!int.TryParse(Value(args, ref i), out var ed)) throw new UsageException("--edition must be an integer.");
                    edition = ed;
                    break;
                case "--tools-dir": toolsDir = Value(args, ref i); break;
                case "--discard": discard = true; break;
                case "--stop-after": stopAfter = Value(args, ref i); break;
                case "--first-boot-route": firstBootRoute = Value(args, ref i); break;
                case "--iso-card": isoCard = Value(args, ref i); break;
                case "--ids":
                    ids = Value(args, ref i).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (ids.Length == 0) throw new UsageException("--ids needs at least one id.");
                    break;
                case var a when a.StartsWith("--", StringComparison.Ordinal):
                    throw new UsageException($"Unknown option '{a}'.");
                default: positional.Add(args[i]); break;
            }
        }
        return new Parsed(card, ids, yes, allow, catalog, source, outIso, edition, toolsDir, discard, stopAfter, firstBootRoute, isoCard, positional);
    }

    private static string Value(string[] args, ref int i)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new UsageException($"{args[i]} needs a value.");
        return args[++i];
    }

    private static void NoPositional(Parsed p)
    {
        if (p.Positional.Count > 0) throw new UsageException($"Unexpected argument '{p.Positional[0]}'.");
    }

    private static CardId ParseCard(string card) => card switch
    {
        _ when card.Equals("recommended", StringComparison.OrdinalIgnoreCase) => CardId.Recommended,
        _ when card.Equals("gamer", StringComparison.OrdinalIgnoreCase) => CardId.Gamer,
        _ when card.Equals("streamer", StringComparison.OrdinalIgnoreCase) => CardId.Streamer,
        _ when card.Equals("max-lean", StringComparison.OrdinalIgnoreCase) => CardId.MaxLean,
        _ => throw new UsageException($"Unknown card '{card}'. Use recommended, gamer, streamer or max-lean."),
    };

    // ---- commands ----

    private static int Detect(Parsed p, TextWriter o, CliEnvironment env)
    {
        NoPositional(p);
        var d = DeviceDetector.Detect(env.Device);
        o.WriteLine($"Maker: {env.Device.Manufacturer}");
        o.WriteLine($"Model: {env.Device.Model}");
        o.WriteLine($"Battery: {YesNo(d.HasBattery)}");
        o.WriteLine($"Touchscreen: {YesNo(d.HasTouchscreen)}");
        o.WriteLine($"Handheld: {YesNo(d.IsHandheld)}");
        if (env.Memory is not null)
        {
            var mem = env.Memory.Read();
            o.WriteLine($"Memory installed: {mem.InstalledMb} MB");
            o.WriteLine($"Memory available to Windows: {mem.VisibleMb} MB");
            o.WriteLine($"Reserved for hardware (mostly the GPU): {mem.ReservedMb} MB");
        }
        return Ok;
    }

    private static int Plan(Parsed p, TextWriter o, CliEnvironment env)
    {
        NoPositional(p);
        var (sel, code) = BuildSelection(p, o, env);
        if (sel is null) return code;
        PrintPlan(sel, o);
        return Ok;
    }

    private static int Apply(Parsed p, TextWriter o, CliEnvironment env, CancellationToken ct)
    {
        NoPositional(p);
        RequireSelectionArgs(p);
        if (!p.Yes)
        {
            o.WriteLine("Refused: apply changes this PC. Run the same command again with --yes to go ahead (use 'plan' to preview it).");
            return Refused;
        }
        var guard = SessionGuard.Check(env.Session);
        if (!guard.Ok)
        {
            o.WriteLine($"Refused: {guard.Reason}");
            return Refused;
        }

        var (sel, code) = BuildSelection(p, o, env);
        if (sel is null) return code;
        PrintDropped(sel, o);
        if (sel.On.Count == 0)
        {
            o.WriteLine("Nothing to apply.");
            return Refused;
        }
        var breaks = WarningEngine.For(sel).Where(w => w.Kind == WarningKind.Breaks).ToList();
        if (breaks.Count > 0)
        {
            o.WriteLine("Warnings:");
            foreach (var w in breaks) o.WriteLine($"  {w.SwitchId}: {w.Message}");
        }

        var store = OpenStore(o, env);
        if (store is null) return Refused;
        var engine = new TuneEngine(env.Systems(), store);
        var options = new TuneOptions(p.AllowNoRestore, env.Session.ProcessUserSid, env.Session.WindowsBuild.ToString());
        var result = engine.Apply(sel, options, new Progress(o), ct);

        if (result.Refused)
        {
            o.WriteLine($"Refused: {result.RefusedReason}");
            if (result.RefusedReason == TuneEngine.NoRestorePointReason)
                o.WriteLine("Use --allow-without-restore-point to go ahead without one.");
            return Refused;
        }

        o.WriteLine(result.RestorePoint == RestorePointOutcome.CreatedUnknown
            ? "Restore point: CreatedUnknown (Windows took the request, but the new point could not be confirmed)"
            : $"Restore point: {result.RestorePoint}");
        foreach (var (id, r) in result.Switches)
        {
            o.WriteLine($"  {id}: {r}");
            foreach (var (_, detail) in result.Problems.Where(x => x.SwitchId == id)) o.WriteLine($"      {detail}");
        }
        if (result.MayBeChanged.Count > 0) o.WriteLine($"May still be changed: {string.Join(", ", result.MayBeChanged)}");
        o.WriteLine($"Journal: {result.JournalPath}");
        if (ct.IsCancellationRequested)
        {
            o.WriteLine("Stopped before every switch was reached. Undo this journal to put the changed ones back.");
            return Refused;
        }
        return result.Switches.Values.Any(r => r == SwitchResult.Failed) ? Refused : Ok;
    }

    private static int Undo(Parsed p, TextWriter o, CliEnvironment env)
    {
        if (p.Positional.Count != 1) throw new UsageException("undo needs one journal (a file path or a run id).");
        if (p.Card is not null || p.Catalog is not null) throw new UsageException("undo does not take --card or --catalog.");
        if (!p.Yes)
        {
            o.WriteLine("Refused: undo changes this PC. Run the same command again with --yes to go ahead.");
            return Refused;
        }
        var guard = SessionGuard.Check(env.Session);
        if (!guard.Ok)
        {
            o.WriteLine($"Refused: {guard.Reason}");
            return Refused;
        }
        var store = OpenStore(o, env);
        if (store is null) return Refused;
        var path = ResolveJournal(p.Positional[0], env);
        if (path is null)
        {
            o.WriteLine($"Refused: journal '{p.Positional[0]}' was not found.");
            return Refused;
        }

        var engine = new TuneEngine(env.Systems(), store);
        var r = engine.Undo(path, p.Ids, env.Session.ProcessUserSid);
        foreach (var id in r.Undone) o.WriteLine($"Undone: {id}");
        foreach (var (id, reason) in r.CannotUndo) o.WriteLine($"Cannot undo: {id}: {reason}");
        foreach (var id in r.NothingToUndo) o.WriteLine($"Nothing to undo: {id}");
        return r.CannotUndo.Count > 0 ? Refused : Ok;
    }

    private static int Drift(Parsed p, TextWriter o, CliEnvironment env)
    {
        if (p.Positional.Count != 1) throw new UsageException("drift needs one journal (a file path or a run id).");
        var store = OpenStore(o, env);
        if (store is null) return Refused;
        var path = ResolveJournal(p.Positional[0], env);
        if (path is null)
        {
            o.WriteLine($"Refused: journal '{p.Positional[0]}' was not found.");
            return Refused;
        }
        var journal = store.Load(path);
        var checker = new DriftChecker(env.Systems());
        var report = checker.Check(journal, env.Session.ProcessUserSid);
        foreach (var (id, err) in report.Errors) o.WriteLine($"Could not check: {id}: {err}");
        if (report.Drifted.Count == 0)
        {
            o.WriteLine("No drift: every applied switch still holds.");
            return Ok;
        }
        foreach (var id in report.Drifted) o.WriteLine($"Drifted: {id}");
        return Refused;
    }

    private static int Health(Parsed p, TextWriter o, CliEnvironment env)
    {
        NoPositional(p);

        if (!string.IsNullOrWhiteSpace(p.IsoCard))
        {
            var card = ParseCard(p.IsoCard);
            var load = CatalogLoader.Load(p.Catalog ?? env.CatalogDir);
            if (!load.Ok)
            {
                o.WriteLine($"Error: Could not load catalog from '{p.Catalog ?? env.CatalogDir}'.");
                return BadArguments;
            }

            var sel = Selection.FromCard(load.Catalog!, card, TargetMode.BuildIso, DeviceProfile.Desktop);
            o.WriteLine($"Health check for ISO preset '{p.IsoCard}' ({sel.On.Count} switches):");
            var applied = new HashSet<string>(sel.On, StringComparer.Ordinal);
            var report = HealthCheck.Run(env.Health, applied);
            foreach (var i in report.Items)
            {
                o.WriteLine($"{(i.Ok ? "OK  " : "FAIL")} {i.Name}{(i.Ok || i.LikelySwitchId is null ? "" : $" (likely: {i.LikelySwitchId})")}");
            }
            return report.AllOk ? Ok : Refused;
        }

        var store = OpenStore(o, env, refuse: false);
        var appliedNormal = new HashSet<string>(StringComparer.Ordinal);
        if (store?.List().FirstOrDefault() is { } latest)
        {
            try
            {
                foreach (var e in store.Load(latest).Entries)
                    if (e.Outcome == ActionOutcome.Applied) appliedNormal.Add(e.SwitchId);
            }
            catch (Exception) { /* unreadable journal: check as if nothing was applied */ }
        }
        var reportNormal = HealthCheck.Run(env.Health, appliedNormal);
        foreach (var i in reportNormal.Items)
            o.WriteLine($"{(i.Ok ? "OK  " : "FAIL")} {i.Name}{(i.Ok || i.LikelySwitchId is null ? "" : $" (likely: {i.LikelySwitchId})")}");
        return reportNormal.AllOk ? Ok : Refused;
    }

    private static int Journals(Parsed p, TextWriter o, CliEnvironment env)
    {
        NoPositional(p);
        var store = OpenStore(o, env);
        if (store is null) return Refused;
        var list = store.List();
        if (list.Count == 0)
        {
            o.WriteLine("No journals.");
            return Ok;
        }
        foreach (var path in list)
        {
            try
            {
                var j = store.Load(path);
                o.WriteLine($"{j.RunId}  {j.SwitchIds.Count} switches  {j.Entries.Count} actions  {path}");
            }
            catch (Exception e)
            {
                o.WriteLine($"{Path.GetFileNameWithoutExtension(path)}  unreadable: {e.Message}  {path}");
            }
        }
        return Ok;
    }

    // ---- helpers ----

    /// <summary>
    /// A journal store whose folder has been secured first. On failure prints why and returns null; with refuse false
    /// (health) it prints a note instead, and the caller goes on without journals.
    /// </summary>
    private static JournalStore? OpenStore(TextWriter o, CliEnvironment env, bool refuse = true)
    {
        var store = new JournalStore(env.JournalDir, () =>
        {
            foreach (var f in env.SecureJournalDir()) o.WriteLine($"Skipped (hard-linked file, left unchanged): {f}");
        });
        try
        {
            store.EnsureDirectory();
            return store;
        }
        catch (Exception e)
        {
            o.WriteLine(refuse
                ? $"Refused: the journal folder could not be secured ({e.Message}). Run nitros from an administrator prompt."
                : $"Note: journals not read, the journal folder could not be secured ({e.Message}).");
            return null;
        }
    }

    private static void RequireSelectionArgs(Parsed p)
    {
        if (p.Card is null && p.Ids is null) throw new UsageException("Give --card <recommended|gamer|streamer|max-lean> or --ids a,b.");
        if (p.Card is not null) ParseCard(p.Card);
    }

    /// <summary>--ids picks exactly those switches (each checked for availability on this device; unavailable ones are dropped). Otherwise --card picks the card's preset.</summary>
    private static (Selection? Selection, int Code) BuildSelection(Parsed p, TextWriter o, CliEnvironment env)
    {
        RequireSelectionArgs(p);
        var dir = p.Catalog ?? env.CatalogDir;
        if (p.Catalog is not null && !Directory.Exists(dir)) throw new UsageException($"Catalog folder '{dir}' not found.");
        var load = CatalogLoader.Load(dir);
        if (load.Catalog is null)
        {
            o.WriteLine($"Refused: the catalog in '{dir}' could not be loaded.");
            foreach (var e in load.Errors) o.WriteLine($"  {e.File}: {e.Message}");
            return (null, Refused);
        }
        var device = DeviceDetector.Detect(env.Device);
        try
        {
            var sel = p.Ids is not null
                ? Selection.FromIds(load.Catalog, p.Ids, TargetMode.TunePc, device)
                : Selection.FromCard(load.Catalog, ParseCard(p.Card!), TargetMode.TunePc, device);
            return (sel, Ok);
        }
        catch (KeyNotFoundException e)
        {
            throw new UsageException(e.Message);
        }
    }

    private static void PrintDropped(Selection sel, TextWriter o)
    {
        if (sel.Dropped.Count == 0) return;
        o.WriteLine("Dropped:");
        foreach (var (id, reason) in sel.Dropped) o.WriteLine($"  {id}: {reason}");
    }

    private static void PrintPlan(Selection sel, TextWriter o)
    {
        var on = sel.Catalog.Switches.Where(s => sel.IsOn(s.Id)).ToList();
        o.WriteLine($"Plan (dry run, nothing is changed): {on.Count} switches, {on.Sum(s => s.Live.Count)} actions");
        foreach (var s in on)
        {
            o.WriteLine($"  [{s.Id}] {s.Name}");
            foreach (var a in s.Live) o.WriteLine($"      {Describe(a)}");
        }
        PrintDropped(sel, o);
        var warnings = WarningEngine.For(sel);
        if (warnings.Count > 0)
        {
            o.WriteLine("Warnings:");
            foreach (var w in warnings) o.WriteLine($"  {w.SwitchId}: {w.Message}");
        }
    }

    private static string Describe(SwitchAction a) => a switch
    {
        RegistrySet r => $"registry.set {r.Hive}\\{r.Key} [{(r.Name.Length == 0 ? "(default)" : r.Name)}] = {r.Data} ({r.Kind})",
        RegistryDelete r => $"registry.delete {r.Hive}\\{r.Key}{(r.Name is null ? " (whole key)" : $" [{r.Name}]")}",
        ServiceStart s => $"service.start {s.Service} -> {s.Start}",
        TaskDisable t => $"task.disable {t.Path}",
        AppxRemove x => $"appx.remove {x.Name}",
        CapabilityRemove c => $"capability.remove {c.Name}",
        FeatureDisable f => $"feature.disable {f.Name}",
        PackageRemove pr => $"package.remove {pr.Pattern}",
        FileRemove f => $"file.remove {f.Path}",
        PowerSet ps => $"power.set {ps.Subgroup}/{ps.Setting} ac={ps.Ac?.ToString() ?? "-"} dc={ps.Dc?.ToString() ?? "-"}",
        PowerScheme ps => $"power.scheme {ps.Scheme}",
        Script => "script",
        ProgramUninstall u => $"program.uninstall {u.Publisher}: {u.DisplayName}",
        StartupDisable s => $"startup.disable {s.Source} {s.Name}",
        LaunchAtLogon l => $"launch.at-logon {l.Program}",
        _ => a.GetType().Name,
    };

    /// <summary>
    /// A journal given as a run id, or as a path that is a direct child of the journal folder (full-path normalised, no junction or
    /// link on the way). Anything outside the folder is a usage error. Null when it is inside but does not exist.
    /// </summary>
    private static string? ResolveJournal(string arg, CliEnvironment env)
    {
        const string outside = "Journals are only read from the NitrOS backups folder.";
        var folder = Path.GetFullPath(env.JournalDir).TrimEnd('\\', '/');
        string full;
        if (arg.IndexOfAny(['\\', '/', ':']) >= 0)
        {
            full = Path.GetFullPath(arg);
            var parent = Path.GetDirectoryName(full)?.TrimEnd('\\', '/');
            if (!string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase)) throw new UsageException(outside);
        }
        else
        {
            if (arg.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || arg is "." or "..") throw new UsageException(outside);
            var name = arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? arg : arg + ".json";
            full = Path.Combine(folder, name);
        }
        if (!File.Exists(full)) return null;
        for (var d = new DirectoryInfo(folder); d is not null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new UsageException(outside);
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new UsageException(outside);
        return full;
    }

    private static int RestoreXbox(Parsed p, TextWriter o, CliEnvironment env, CancellationToken ct)
    {
        NoPositional(p);
        if (!p.Yes)
        {
            o.WriteLine("Refused: restore-xbox modifies this PC. Run the same command again with --yes to go ahead.");
            return Refused;
        }
        var guard = SessionGuard.Check(env.Session);
        if (!guard.Ok)
        {
            o.WriteLine($"Refused: {guard.Reason}");
            return Refused;
        }

        var catalogDir = p.Catalog ?? env.CatalogDir;
        var load = CatalogLoader.Load(catalogDir);
        var catalog = load.Ok ? load.Catalog : null;

        var restorer = env.XboxRestore is not null
            ? env.XboxRestore(env.Systems(), catalog)
            : new XboxRestore(env.Systems(), new WinXboxRestore(), catalog);

        var progress = new Progress<string>(o.WriteLine);
        var result = restorer.Run(progress, ct);

        foreach (var part in result.Parts)
        {
            var status = part.Ok ? "OK" : "FAILED";
            var detail = string.IsNullOrWhiteSpace(part.Detail) ? "" : $" ({part.Detail})";
            o.WriteLine($"  {part.Part}: {status}{detail}");
        }

        if (!result.Ok)
        {
            o.WriteLine("Restore Xbox failed: not all parts were restored.");
            return Refused;
        }

        o.WriteLine("Restore Xbox completed successfully.");
        return Ok;
    }

    private static int ImageInfo(Parsed p, TextWriter o, CliEnvironment env)
    {
        NoPositional(p);
        if (string.IsNullOrWhiteSpace(p.Source))
        {
            throw new UsageException("image-info requires --source <iso|folder>.");
        }

        var imgSystems = env.Image != null ? env.Image() : WinImageSystems.Create();
        using var mount = imgSystems.Media.Open(p.Source);
        var installWim = Path.Combine(mount.Root, "sources", "install.wim");
        if (!imgSystems.Files.Exists(installWim))
        {
            installWim = Path.Combine(mount.Root, "sources", "install.esd");
        }

        var editions = imgSystems.Dism.Editions(installWim);
        o.WriteLine($"Editions in '{p.Source}':");
        foreach (var ed in editions)
        {
            o.WriteLine($"  Index {ed.Index}: {ed.Name} (Edition: {ed.EditionId}, Arch: {ed.Architecture}, Version: {ed.Version})");
        }
        return Ok;
    }

    private static int BuildIso(Parsed p, TextWriter o, CliEnvironment env, CancellationToken ct)
    {
        NoPositional(p);

        if (p.Discard)
        {
            if (string.IsNullOrWhiteSpace(p.Out)) throw new UsageException("build-iso --discard requires --out <iso>.");
            if (!p.Yes)
            {
                o.WriteLine("Refused: discard removes work files. Run with --yes to go ahead.");
                return Refused;
            }

            var guard = SessionGuard.Check(env.Session);
            if (!guard.Ok)
            {
                o.WriteLine($"Refused: {guard.Reason}");
                return Refused;
            }

            var imgSystems = env.Image != null ? env.Image() : WinImageSystems.Create();
            var outFull = Path.GetFullPath(p.Out);
            var drive = Path.GetPathRoot(outFull) ?? "C:\\";
            var existingWork = imgSystems.Files.SubFolders(drive)
                .FirstOrDefault(d => Path.GetFileName(d).StartsWith("NitrOS-work-", StringComparison.OrdinalIgnoreCase) &&
                                     new BuildStore(d).TryLoad() is { } st &&
                                     string.Equals(st.Request.OutputIso, p.Out, StringComparison.OrdinalIgnoreCase));

            if (existingWork != null)
            {
                imgSystems.Files.DeleteWorkTree(existingWork);
                o.WriteLine($"Discarded unfinished build for '{p.Out}'.");
            }
            else
            {
                o.WriteLine($"No unfinished build found for '{p.Out}'.");
            }
            return Ok;
        }

        // Must have --yes
        if (!p.Yes)
        {
            o.WriteLine("Refused: build-iso creates a new Windows installation media. Run the same command again with --yes to go ahead.");
            return Refused;
        }

        // Diagnostic option checks
        var hasDiag = !string.IsNullOrWhiteSpace(p.StopAfter) || !string.IsNullOrWhiteSpace(p.FirstBootRoute);
        var isVm = string.Equals(Environment.GetEnvironmentVariable("NITROS_VM"), "1", StringComparison.Ordinal);
        if (hasDiag && !isVm)
        {
            o.WriteLine("Error: Diagnostic options are only permitted when NITROS_VM=1.");
            return BadArguments;
        }

        if (string.IsNullOrWhiteSpace(p.Source) || string.IsNullOrWhiteSpace(p.Out))
        {
            throw new UsageException("build-iso requires both --source and --out.");
        }

        if (p.Card != null && p.Ids != null)
        {
            throw new UsageException("build-iso requires either --card or --ids, not both.");
        }
        if (p.Card == null && p.Ids == null)
        {
            throw new UsageException("build-iso requires either --card or --ids.");
        }

        var guardIso = SessionGuard.Check(env.Session);
        if (!guardIso.Ok)
        {
            o.WriteLine($"Refused: {guardIso.Reason}");
            return Refused;
        }

        var catDir = p.Catalog ?? env.CatalogDir;
        var load = CatalogLoader.Load(catDir);
        if (!load.Ok)
        {
            o.WriteLine($"Error: Could not load catalog from '{catDir}'.");
            return BadArguments;
        }

        var catalog = load.Catalog!;
        var dev = DeviceDetector.Detect(env.Device);
        var toolsDir = p.ToolsDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NitrOS", "tools");
        var oscdimg = env.Oscdimg != null ? env.Oscdimg(toolsDir) : new Oscdimg(toolsDir, new OscdimgDownloader(new HttpGet()));
        var imgSystemsReal = env.Image != null ? env.Image() : WinImageSystems.Create();

        Selection sel;
        CardId? cardId = null;
        if (p.Card != null)
        {
            cardId = ParseCard(p.Card);
            sel = Selection.FromCard(catalog, cardId.Value, TargetMode.BuildIso, dev);
        }
        else
        {
            sel = Selection.FromIds(catalog, p.Ids!, TargetMode.BuildIso, dev);
        }

        Func<string, BuildStore> storeFactory = folder => new BuildStore(folder);
        var imageBuilder = new ImageBuilder(imgSystemsReal, catalog, dev, storeFactory, oscdimg);
        var existing = imageBuilder.FindResumable(p.Out);
        BuildState state;
        if (existing != null)
        {
            o.WriteLine($"Resuming existing ISO build for '{p.Out}'...");
            state = existing;
        }
        else
        {
            var req = new BuildRequest(p.Source, p.Out, p.Edition, sel.On.ToList(), cardId);
            try
            {
                state = imageBuilder.Start(req);
            }
            catch (BuildRefusedException bre)
            {
                o.WriteLine($"Refused: {bre.Message}");
                return Refused;
            }
        }

        var progress = new Progress<BuildProgress>(bp =>
        {
            if (bp.LogLine != null) o.WriteLine($"  {bp.LogLine}");
        });

        var res = imageBuilder.Run(state, progress, ct);
        if (res.Status == BuildStatus.Refused)
        {
            o.WriteLine($"Refused: {res.Reason}");
            return Refused;
        }
        if (res.Status != BuildStatus.Done)
        {
            o.WriteLine($"Build finished with status {res.Status}: {res.Reason}");
            return Refused;
        }

        foreach (var (id, sr) in res.Switches)
        {
            o.WriteLine($"  [{id}] {sr}");
        }
        o.WriteLine($"Output: {res.OutputPath}");
        return Ok;
    }

    private static string YesNo(bool b) => b ? "yes" : "no";

    private sealed class Progress(TextWriter o) : IProgress<TuneProgress>
    {
        public void Report(TuneProgress v) => o.WriteLine($"  {v.Done}/{v.Total} {v.SwitchId}");
    }
}
