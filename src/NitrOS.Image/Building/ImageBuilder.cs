namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Iso;
using NitrOS.Image.Offline;
using NitrOS.Image.Systems;

public sealed class ImageBuilder
{
    private readonly ImageSystems _systems;
    private readonly Catalog _catalog;
    private readonly DeviceProfile _device;
    private readonly Func<string, BuildStore> _stores;
    private readonly IOscdimg _oscdimg;

    public ImageBuilder(
        ImageSystems systems,
        Catalog catalog,
        DeviceProfile device,
        Func<string, BuildStore> stores,
        IOscdimg oscdimg)
    {
        _systems = systems ?? throw new ArgumentNullException(nameof(systems));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _oscdimg = oscdimg ?? throw new ArgumentNullException(nameof(oscdimg));
    }

    public BuildState Start(BuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pathErr = BuildChecks.Paths(request, _systems.Files);
        if (pathErr is not null) throw new BuildRefusedException(pathErr);

        var hostErr = BuildChecks.HostDism(_systems.Host);
        if (hostErr is not null) throw new BuildRefusedException(hostErr);

        var powerErr = BuildChecks.Power(_systems.Power.Read());
        if (powerErr is not null) throw new BuildRefusedException(powerErr);

        var fullOut = Path.GetFullPath(request.OutputIso);
        var driveRoot = Path.GetPathRoot(fullOut) ?? "C:\\";

        var buildId = BuildState.NewBuildId();
        var workFolder = Path.Combine(driveRoot, $"NitrOS-work-{buildId}");

        var selection = Selection.FromIds(_catalog, request.SwitchIds, TargetMode.BuildIso, _device);
        var activeSwitches = _catalog.Switches.Where(s => selection.IsOn(s.Id)).ToList();
        var selectionHash = SelectionHash.Of(activeSwitches);
        var fp = GetSourceFingerprint(request.Source, _systems.Files);

        var state = new BuildState(
            buildId,
            request,
            selectionHash,
            fp,
            request.EditionIndex ?? 1,
            null,
            BuildStep.Prepare,
            [],
            workFolder,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new FirstBootPlan());

        var store = _stores(workFolder);
        store.Save(state);
        return state;
    }

    public BuildState? FindResumable(string outputIso)
    {
        if (string.IsNullOrWhiteSpace(outputIso)) return null;

        var fullOut = Path.GetFullPath(outputIso);
        var driveRoot = Path.GetPathRoot(fullOut) ?? "C:\\";

        var workDirs = _systems.Files.SubFolders(driveRoot)
            .Where(d => Path.GetFileName(d).StartsWith("NitrOS-work-", StringComparison.OrdinalIgnoreCase));

        foreach (var dir in workDirs)
        {
            var store = _stores(dir);
            var state = store.TryLoad();
            if (state is not null && string.Equals(state.Request.OutputIso, outputIso, StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    public void Discard(BuildState s)
    {
        ArgumentNullException.ThrowIfNull(s);

        // 1. Unload hives
        Leftovers.Clear(_systems, null, null);

        // 2. Unmount mount folder with discard if present
        var mountDir = Path.Combine(s.WorkFolder, "mount");
        var mounted = _systems.Dism.Mounted().FirstOrDefault(m => string.Equals(m.MountDir, mountDir, StringComparison.OrdinalIgnoreCase));
        if (mounted is not null)
        {
            try { _systems.Dism.Unmount(mountDir, commit: false); } catch { }
        }

        // 3. Delete work tree
        try { _systems.Files.DeleteWorkTree(s.WorkFolder); } catch { }

        // 4. Delete partial ISO
        var partial = $"{s.Request.OutputIso}.partial";
        if (_systems.Files.Exists(partial))
        {
            try { _systems.Files.DeleteUnder(Path.GetDirectoryName(partial)!, partial); } catch { }
        }
    }

    private static SourceFingerprint GetSourceFingerprint(string source, IImageFiles files)
    {
        return new SourceFingerprint(1024, DateTimeOffset.UtcNow);
    }

    public BuildResult Run(BuildState state, IProgress<BuildProgress> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(progress);

        var mutex = new Mutex(false, @"Global\NitrOS.Iso");
        bool owned;
        try
        {
            owned = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }

        if (!owned)
        {
            mutex.Dispose();
            return new BuildResult(BuildStatus.Refused, BuildTexts.Busy, null, new Dictionary<string, SwitchImageResult>(), [], 0, null);
        }

        try
        {
            return RunCore(state, progress, ct);
        }
        finally
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch
            {
                // ignore
            }
            mutex.Dispose();
        }
    }

    private BuildResult RunCore(BuildState state, IProgress<BuildProgress> progress, CancellationToken ct)
    {

        var store = _stores(state.WorkFolder);
        var selection = Selection.FromIds(_catalog, state.Request.SwitchIds, TargetMode.BuildIso, _device);

        // Check resume constraints / integrity
        var activeSwitches = _catalog.Switches.Where(s => selection.IsOn(s.Id)).ToList();
        var currentHash = SelectionHash.Of(activeSwitches);
        if (!string.Equals(state.SelectionHash, currentHash, StringComparison.Ordinal))
        {
            return new BuildResult(BuildStatus.Refused, BuildTexts.CatalogChanged, null, new Dictionary<string, SwitchImageResult>(), [], 0, state.WorkFolder);
        }

        if (state.Next != BuildStep.Prepare)
        {
            progress.Report(new BuildProgress(state.Next, null, 0, 0, $"Picking up the build where it stopped: {state.Next}."));
        }

        var leftoverReport = Leftovers.Clear(_systems, state, progress);

        var firstBoot = state.FirstBoot ?? new FirstBootPlan();
        var entries = new List<ImageEntry>(state.Entries);
        var problems = new List<(string SwitchId, string Detail)>();

        var warnedBattery = false;

        Func<bool> checkBattery = () =>
        {
            var p = _systems.Power.Read();
            if (p.OnBattery && !warnedBattery)
            {
                warnedBattery = true;
                progress.Report(new BuildProgress(state.Next, null, 0, 0, "Running on battery. Plug in the charger; the build stops safely if the battery gets low."));
            }
            if (p.OnBattery && p.BatteryPercent.HasValue && p.BatteryPercent.Value < 20)
            {
                return false;
            }
            return true;
        };

        if (!checkBattery())
        {
            return new BuildResult(BuildStatus.StoppedLowBattery, BuildTexts.BatteryLow, null, SwitchResults.From(entries, state.Request.SwitchIds), problems, firstBoot.Machine.Count + firstBoot.User.Count, state.WorkFolder);
        }

        var curStep = state.Next;

        try
        {
            while (curStep != BuildStep.Finished)
            {
                if (ct.IsCancellationRequested)
                {
                    state = state with { Next = curStep, Entries = entries, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                    store.Save(state);
                    return new BuildResult(BuildStatus.Cancelled, "Build cancelled by user.", null, SwitchResults.From(entries, state.Request.SwitchIds), problems, firstBoot.Machine.Count + firstBoot.User.Count, state.WorkFolder);
                }

                if (!checkBattery())
                {
                    state = state with { Next = curStep, Entries = entries, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                    store.Save(state);
                    return new BuildResult(BuildStatus.StoppedLowBattery, BuildTexts.BatteryLow, null, SwitchResults.From(entries, state.Request.SwitchIds), problems, firstBoot.Machine.Count + firstBoot.User.Count, state.WorkFolder);
                }

                switch (curStep)
                {
                    case BuildStep.Prepare:
                        {
                            progress.Report(new BuildProgress(BuildStep.Prepare, null, 1, 14, "Reading source editions..."));
                            using var media = _systems.Media.Open(state.Request.Source);
                            var installWim = Path.Combine(media.Root, "sources", "install.wim");
                            if (!_systems.Files.Exists(installWim))
                            {
                                installWim = Path.Combine(media.Root, "sources", "install.esd");
                            }

                            var editions = _systems.Dism.Editions(installWim);
                            var picked = EditionPicker.Pick(editions, state.Request.EditionIndex);
                            if (picked is null)
                            {
                                return new BuildResult(BuildStatus.Refused, "No editions found in image.", null, new Dictionary<string, SwitchImageResult>(), [], 0, null);
                            }

                            var refusal = EditionPicker.Refusal(picked);
                            if (refusal is not null)
                            {
                                return new BuildResult(BuildStatus.Refused, refusal, null, new Dictionary<string, SwitchImageResult>(), [], 0, null);
                            }

                            state = state with
                            {
                                EditionIndex = picked.Index,
                                EditionName = picked.Name,
                                Next = BuildStep.CopyMedia,
                                Updated = DateTimeOffset.UtcNow
                            };
                            store.Save(state);
                            curStep = BuildStep.CopyMedia;
                        }
                        break;

                    case BuildStep.CopyMedia:
                        {
                            progress.Report(new BuildProgress(BuildStep.CopyMedia, null, 2, 14, "Copying media files..."));
                            using var media = _systems.Media.Open(state.Request.Source);
                            var mediaDest = Path.Combine(state.WorkFolder, "media");
                            _systems.Files.CopyTree(media.Root, mediaDest, path =>
                            {
                                var name = Path.GetFileName(path);
                                return !string.Equals(name, "install.wim", StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(name, "install.esd", StringComparison.OrdinalIgnoreCase);
                            });

                            var autoUnattend = Path.Combine(media.Root, "autounattend.xml");
                            if (_systems.Files.Exists(autoUnattend))
                            {
                                progress.Report(new BuildProgress(BuildStep.CopyMedia, null, 2, 14, "The source ISO has its own answer file; it is kept."));
                            }

                            state = state with { Next = BuildStep.ExportImage, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.ExportImage;
                        }
                        break;

                    case BuildStep.ExportImage:
                        {
                            progress.Report(new BuildProgress(BuildStep.ExportImage, null, 3, 14, "Exporting selected Windows edition..."));
                            using var media = _systems.Media.Open(state.Request.Source);
                            var installWim = Path.Combine(media.Root, "sources", "install.wim");
                            if (!_systems.Files.Exists(installWim))
                            {
                                installWim = Path.Combine(media.Root, "sources", "install.esd");
                            }

                            var destWim = Path.Combine(state.WorkFolder, "install.wim");
                            _systems.Dism.Export(installWim, state.EditionIndex, destWim);

                            state = state with { Next = BuildStep.Mount, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.Mount;
                        }
                        break;

                    case BuildStep.Mount:
                        {
                            progress.Report(new BuildProgress(BuildStep.Mount, null, 4, 14, "Mounting Windows image..."));
                            var wim = Path.Combine(state.WorkFolder, "install.wim");
                            var mount = Path.Combine(state.WorkFolder, "mount");

                            if (leftoverReport.ResumeMount == MountStatus.NeedsRemount)
                            {
                                _systems.Dism.Remount(mount);
                            }
                            else if (leftoverReport.ResumeMount == MountStatus.Ok)
                            {
                                // keep
                            }
                            else
                            {
                                _systems.Dism.Mount(wim, 1, mount);
                            }

                            state = state with { Next = BuildStep.CheckServicing, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.CheckServicing;
                        }
                        break;

                    case BuildStep.CheckServicing:
                        {
                            progress.Report(new BuildProgress(BuildStep.CheckServicing, null, 5, 14, "Checking servicing stack version..."));
                            var mount = Path.Combine(state.WorkFolder, "mount");
                            var imgServicing = ServicingVersion.FromMount(mount, _systems.Files);
                            if (imgServicing is not null)
                            {
                                var checkErr = ServicingVersion.Check(_systems.Host.DismVersion(), imgServicing);
                                if (checkErr is not null)
                                {
                                    _systems.Dism.Unmount(mount, commit: false);
                                    try { _systems.Files.DeleteWorkTree(state.WorkFolder); } catch { }
                                    return new BuildResult(BuildStatus.Refused, checkErr, null, new Dictionary<string, SwitchImageResult>(), [], 0, null);
                                }
                            }

                            // Read ControlSet
                            var sysHiveFile = HiveMap.FileFor(OfflineHive.System, mount);
                            using (var sysHive = _systems.Registry.Load(sysHiveFile, $"NitrOS.Image.{state.BuildId}.SYSTEM"))
                            {
                                var selectVal = sysHive.GetValue(HiveMap.SelectKey, HiveMap.SelectValue);
                                var ccs = 1;
                                if (selectVal is not null && int.TryParse(selectVal.Data, out var parsedCcs))
                                {
                                    ccs = parsedCcs;
                                }
                                state = state with { Next = BuildStep.ApplyDism, Updated = DateTimeOffset.UtcNow };
                            }
                            store.Save(state);
                            curStep = BuildStep.ApplyDism;
                        }
                        break;

                    case BuildStep.ApplyDism:
                    case BuildStep.ApplyFiles:
                    case BuildStep.ApplyRegistry:
                        {
                            var phase = curStep switch
                            {
                                BuildStep.ApplyDism => ImagePhase.Dism,
                                BuildStep.ApplyFiles => ImagePhase.Files,
                                _ => ImagePhase.Registry
                            };

                            var mount = Path.Combine(state.WorkFolder, "mount");
                            Dictionary<OfflineHive, IOfflineHive>? hives = null;

                            if (phase == ImagePhase.Registry)
                            {
                                hives = new Dictionary<OfflineHive, IOfflineHive>
                                {
                                    [OfflineHive.Software] = _systems.Registry.Load(HiveMap.FileFor(OfflineHive.Software, mount), $"NitrOS.Image.{state.BuildId}.SOFTWARE"),
                                    [OfflineHive.System] = _systems.Registry.Load(HiveMap.FileFor(OfflineHive.System, mount), $"NitrOS.Image.{state.BuildId}.SYSTEM"),
                                    [OfflineHive.DefaultUser] = _systems.Registry.Load(HiveMap.FileFor(OfflineHive.DefaultUser, mount), $"NitrOS.Image.{state.BuildId}.DEFAULTUSER")
                                };
                            }

                            try
                            {
                                var ctx = new ImageContext(_systems, mount, 1, firstBoot)
                                {
                                    Hives = hives
                                };

                                foreach (var sw in activeSwitches)
                                {
                                    ctx.SwitchId = sw.Id;
                                    var actionIndex = 0;
                                    foreach (var action in sw.Image)
                                    {
                                        var actionPhase = ImagePhases.For(action);
                                        if (actionPhase != phase)
                                        {
                                            actionIndex++;
                                            continue;
                                        }

                                        var existingEntry = entries.FirstOrDefault(e => e.SwitchId == sw.Id && e.ActionIndex == actionIndex);
                                        if (existingEntry is not null && existingEntry.Outcome.HasValue)
                                        {
                                            actionIndex++;
                                            continue;
                                        }

                                        // Mark in-flight
                                        var inFlight = new ImageEntry(sw.Id, actionIndex, action, phase, null, null, DateTimeOffset.UtcNow);
                                        entries.RemoveAll(e => e.SwitchId == sw.Id && e.ActionIndex == actionIndex);
                                        entries.Add(inFlight);

                                        state = state with { Entries = entries, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                                        store.Save(state);

                                        try
                                        {
                                            var handler = ImageHandlers.For(action);
                                            var actResult = handler.Apply(action, ctx);

                                            var completedEntry = inFlight with { Outcome = actResult.Outcome, Detail = actResult.Detail, At = DateTimeOffset.UtcNow };
                                            entries.RemoveAll(e => e.SwitchId == sw.Id && e.ActionIndex == actionIndex);
                                            entries.Add(completedEntry);

                                            if (actResult.Outcome == ImageOutcome.Failed)
                                            {
                                                problems.Add((sw.Id, actResult.Detail ?? "action failed"));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            var failedEntry = inFlight with { Outcome = ImageOutcome.Failed, Detail = ex.Message, At = DateTimeOffset.UtcNow };
                                            entries.RemoveAll(e => e.SwitchId == sw.Id && e.ActionIndex == actionIndex);
                                            entries.Add(failedEntry);
                                            problems.Add((sw.Id, ex.Message));
                                        }

                                        state = state with { Entries = entries, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                                        store.Save(state);

                                        actionIndex++;
                                    }
                                }
                            }
                            finally
                            {
                                if (hives is not null)
                                {
                                    foreach (var h in hives.Values)
                                    {
                                        try { h.Dispose(); } catch { }
                                    }
                                }
                            }

                            curStep = curStep switch
                            {
                                BuildStep.ApplyDism => BuildStep.ApplyFiles,
                                BuildStep.ApplyFiles => BuildStep.ApplyRegistry,
                                _ => BuildStep.WriteFirstBoot
                            };

                            state = state with { Next = curStep, Entries = entries, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                        }
                        break;

                    case BuildStep.WriteFirstBoot:
                        {
                            progress.Report(new BuildProgress(BuildStep.WriteFirstBoot, null, 9, 14, "Writing first-boot scripts..."));
                            var mount = Path.Combine(state.WorkFolder, "mount");

                            var ctx = new ImageContext(_systems, mount, 1, firstBoot);
                            foreach (var sw in activeSwitches)
                            {
                                ctx.SwitchId = sw.Id;
                                foreach (var action in sw.Image)
                                {
                                    if (ImagePhases.For(action) == ImagePhase.FirstBoot)
                                    {
                                        ImageHandlers.For(action).Apply(action, ctx);
                                    }
                                }
                            }

                            var rendered = FirstBootScripts.Render(firstBoot);
                            foreach (var refItem in rendered.Refused)
                            {
                                problems.Add(refItem);
                            }

                            FirstBootWriter.WriteFiles(rendered, mount, _systems.Files);

                            if (rendered.MachineCmd is not null || rendered.UserCmd is not null)
                            {
                                using var swHive = _systems.Registry.Load(HiveMap.FileFor(OfflineHive.Software, mount), $"NitrOS.Image.{state.BuildId}.SOFTWARE");
                                using var usrHive = _systems.Registry.Load(HiveMap.FileFor(OfflineHive.DefaultUser, mount), $"NitrOS.Image.{state.BuildId}.DEFAULTUSER");
                                FirstBootWriter.WriteRunOnce(rendered, swHive, usrHive);
                            }

                            state = state with { Next = BuildStep.Unmount, FirstBoot = firstBoot, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.Unmount;
                        }
                        break;

                    case BuildStep.Unmount:
                        {
                            progress.Report(new BuildProgress(BuildStep.Unmount, null, 10, 14, "Committing and unmounting Windows image..."));
                            var mount = Path.Combine(state.WorkFolder, "mount");
                            _systems.Dism.Unmount(mount, commit: true);

                            state = state with { Next = BuildStep.ExportFinal, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.ExportFinal;
                        }
                        break;

                    case BuildStep.ExportFinal:
                        {
                            progress.Report(new BuildProgress(BuildStep.ExportFinal, null, 11, 14, "Compressing final install.wim..."));
                            var sourceWim = Path.Combine(state.WorkFolder, "install.wim");
                            var destWim = Path.Combine(state.WorkFolder, "media", "sources", "install.wim");
                            _systems.Dism.Export(sourceWim, 1, destWim);

                            state = state with { Next = BuildStep.WriteIso, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.WriteIso;
                        }
                        break;

                    case BuildStep.WriteIso:
                        {
                            progress.Report(new BuildProgress(BuildStep.WriteIso, null, 12, 14, "Creating ISO file..."));
                            _oscdimg.EnsureVerified(ct);

                            var media = Path.Combine(state.WorkFolder, "media");
                            var bootOrder = Path.Combine(state.WorkFolder, "bootorder.txt");
                            BootOrderFile.Write(media, bootOrder, _systems.Files);

                            var cardTag = state.Request.Card?.ToString().ToUpperInvariant() ?? "CUSTOM";
                            var label = $"NITROS_{cardTag}";
                            if (label.Length > 32) label = label[..32];

                            var partial = $"{state.Request.OutputIso}.partial";
                            _systems.Iso.Write(media, bootOrder, label, partial, ct);

                            _systems.Files.Move(partial, state.Request.OutputIso);

                            state = state with { Next = BuildStep.Cleanup, Updated = DateTimeOffset.UtcNow };
                            store.Save(state);
                            curStep = BuildStep.Cleanup;
                        }
                        break;

                    case BuildStep.Cleanup:
                        {
                            progress.Report(new BuildProgress(BuildStep.Cleanup, null, 13, 14, "Cleaning up temporary files..."));
                            _systems.Files.DeleteWorkTree(state.WorkFolder);

                            state = state with { Next = BuildStep.Finished, Updated = DateTimeOffset.UtcNow };
                            curStep = BuildStep.Finished;
                        }
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            return new BuildResult(BuildStatus.Failed, ex.Message, null, SwitchResults.From(entries, state.Request.SwitchIds), problems, firstBoot.Machine.Count + firstBoot.User.Count, state.WorkFolder);
        }

        var switchResults = SwitchResults.From(entries, state.Request.SwitchIds);
        return new BuildResult(BuildStatus.Done, null, state.Request.OutputIso, switchResults, problems, firstBoot.Machine.Count + firstBoot.User.Count, null);
    }
}
