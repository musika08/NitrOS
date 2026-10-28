namespace NitrOS.App.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NitrOS.App.Logic;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

public sealed class IsoRunnerTests
{
    private sealed class FakeIsoBackend : IIsoBackend
    {
        public Func<BuildRequest, string?> PathsReasonFunc { get; set; } = _ => null;
        public Func<string?> HostDismReasonFunc { get; set; } = () => null;
        public Func<PowerState> PowerFunc { get; set; } = () => new PowerState(false, false, 100);
        public Func<string, IReadOnlyList<ImageEdition>> EditionsFunc { get; set; } = _ => [];
        public Func<string, BuildState?> FindResumableFunc { get; set; } = _ => null;
        public Func<BuildRequest, BuildState> StartFunc { get; set; } = r =>
            new BuildState("123", r, "hash", new SourceFingerprint(1024, DateTimeOffset.UtcNow), 1, "Professional", BuildStep.ExportImage, [], "C:\\NitrOS-work-123", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new NitrOS.Image.FirstBoot.FirstBootPlan());
        public Func<BuildState, IProgress<BuildProgress>, CancellationToken, BuildResult> RunFunc { get; set; } =
            (s, p, ct) => new BuildResult(BuildStatus.Done, null, s.Request.OutputIso, new Dictionary<string, SwitchImageResult>(), [], 0, null);

        public int DiscardCalls { get; private set; }

        public IReadOnlyList<ImageEdition> Editions(string source) => EditionsFunc(source);
        public BuildState? FindResumable(string outputIso) => FindResumableFunc(outputIso);
        public BuildState Start(BuildRequest request) => StartFunc(request);
        public BuildResult Run(BuildState state, IProgress<BuildProgress> progress, CancellationToken ct) => RunFunc(state, progress, ct);
        public void Discard(BuildState state) => DiscardCalls++;
        public string? HostDismReason() => HostDismReasonFunc();
        public PowerState Power() => PowerFunc();
        public string? PathsReason(BuildRequest request) => PathsReasonFunc(request);
    }

    private static readonly Catalog TestCatalogInstance = TestCatalog.LoadRepo();
    private static readonly string TestSwitchId = TestCatalogInstance.Switches[0].Id;

    private static (FakeIsoBackend Backend, IsoRunner Runner, IsoBuildIndex Index, string TempDir) CreateRunner(bool elevated = true)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros-isorunner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var backend = new FakeIsoBackend();
        var session = new FakeSession(elevated: elevated);
        var index = new IsoBuildIndex(tempDir, _ => FolderState.Missing, p => Directory.CreateDirectory(p), session);
        var runner = new IsoRunner(backend, session, index);

        return (backend, runner, index, tempDir);
    }

    [Fact]
    public void IsoRunner_NotReadyWithoutPaths()
    {
        var (backend, runner, _, dir) = CreateRunner();
        try
        {
            var sel = Selection.FromIds(TestCatalogInstance, [], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, null);

            var ready = runner.CheckReady(req);
            Assert.False(ready.Ready);
            Assert.Equal(Texts.IsoChoosePaths, ready.Reason);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsoRunner_NotReadyWhenDismTooOld()
    {
        var (backend, runner, _, dir) = CreateRunner();
        try
        {
            backend.HostDismReasonFunc = () => "Host DISM is too old.";
            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            var ready = runner.CheckReady(req);
            Assert.False(ready.Ready);
            Assert.Equal("Host DISM is too old.", ready.Reason);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsoRunner_NotReadyOnBattery()
    {
        var (backend, runner, _, dir) = CreateRunner();
        try
        {
            backend.PowerFunc = () => new PowerState(true, true, 10);
            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            var ready = runner.CheckReady(req);
            Assert.False(ready.Ready);
            Assert.Contains("battery", ready.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsoRunner_CheckReadyHasNoSideEffects()
    {
        var (backend, runner, index, dir) = CreateRunner();
        try
        {
            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            _ = runner.CheckReady(req);
            Assert.Null(index.Read());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoRunner_RefusedSessionDoesNothing()
    {
        var (backend, runner, index, dir) = CreateRunner(elevated: false);
        try
        {
            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            var result = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);
            Assert.True(result.Refused);
            Assert.Null(index.Read());
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoRunner_MapsResultAndOutputPath()
    {
        var (backend, runner, index, dir) = CreateRunner();
        try
        {
            backend.RunFunc = (s, p, ct) =>
            {
                var dict = new Dictionary<string, SwitchImageResult>
                {
                    [TestSwitchId] = SwitchImageResult.Applied
                };
                return new BuildResult(BuildStatus.Done, null, "C:\\out.iso", dict, [], 0, null);
            };

            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            var result = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);
            Assert.False(result.Refused);
            Assert.Equal("C:\\out.iso", result.OutputPath);
            Assert.Equal(SwitchOutcome.Applied, result.Switches[TestSwitchId]);
            Assert.Null(index.Read()); // Cleared on Done
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoRunner_ResumesWhenStateExists()
    {
        var (backend, runner, index, dir) = CreateRunner();
        try
        {
            var startCalled = false;
            backend.StartFunc = r =>
            {
                startCalled = true;
                return new BuildState("new", r, "hash", new SourceFingerprint(1024, DateTimeOffset.UtcNow), 1, "Professional", BuildStep.ExportImage, [], "C:\\NitrOS-work-new", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new NitrOS.Image.FirstBoot.FirstBootPlan());
            };

            var existingState = new BuildState("resumed-123", new BuildRequest("C:\\src.iso", "C:\\out.iso", 1, [TestSwitchId], CardId.Recommended), "hash", new SourceFingerprint(1024, DateTimeOffset.UtcNow), 1, "Professional", BuildStep.ApplyRegistry, [], "C:\\NitrOS-work-resumed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new NitrOS.Image.FirstBoot.FirstBootPlan());
            backend.FindResumableFunc = _ => existingState;

            BuildState? passedToRun = null;
            backend.RunFunc = (s, p, ct) =>
            {
                passedToRun = s;
                return new BuildResult(BuildStatus.Done, null, "C:\\out.iso", new Dictionary<string, SwitchImageResult>(), [], 0, null);
            };

            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            _ = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);

            Assert.False(startCalled);
            Assert.Same(existingState, passedToRun);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoRunner_CancelledKeepsIndex()
    {
        var (backend, runner, index, dir) = CreateRunner();
        try
        {
            backend.RunFunc = (s, p, ct) =>
                new BuildResult(BuildStatus.Cancelled, "Cancelled by user", null, new Dictionary<string, SwitchImageResult>(), [], 0, s.WorkFolder);

            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            var result = await runner.RunAsync(req, new Progress<RunProgress>(), CancellationToken.None);
            Assert.True(result.Cancelled);
            Assert.NotNull(index.Read()); // Index retained for resume
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoRunner_ResumeLineReachesWorkingLog()
    {
        var (backend, runner, _, dir) = CreateRunner();
        try
        {
            var logs = new List<string>();
            var prog = new Progress<RunProgress>(p =>
            {
                if (p.LogLine != null) logs.Add(p.LogLine);
            });

            backend.RunFunc = (s, p, ct) =>
            {
                p.Report(new BuildProgress(BuildStep.ExportImage, null, 1, 10, "Resuming build step"));
                return new BuildResult(BuildStatus.Done, null, "C:\\out.iso", new Dictionary<string, SwitchImageResult>(), [], 0, null);
            };

            var sel = Selection.FromIds(TestCatalogInstance, [TestSwitchId], TargetMode.BuildIso, DeviceProfile.Desktop);
            var req = new RunRequest(RunKind.Iso, sel, CardId.Recommended, false, new IsoPaths("C:\\src.iso", "C:\\out.iso"));

            _ = await runner.RunAsync(req, prog, CancellationToken.None);
            Assert.Contains("Resuming build step", logs);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
