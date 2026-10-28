namespace NitrOS.App.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NitrOS.App.Logic.Flow;
using NitrOS.App.Logic.Navigation;
using NitrOS.App.Logic.Ports;
using NitrOS.App.Logic.Running;
using NitrOS.App.Logic.Screens;
using NitrOS.App.Logic.Storage;
using NitrOS.App.Tests.Fakes;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Live.Windows;

public sealed class IsoSourceViewModelTests
{
    private sealed class FakeFilePicker : IFilePicker
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public Task<string?> PickIsoToOpenAsync() => Task.FromResult(OpenPath);
        public Task<string?> PickIsoToSaveAsync(string suggestedName) => Task.FromResult(SavePath);
    }

    private sealed class FakeIsoBackend : IIsoBackend
    {
        public Func<BuildRequest, string?> PathsReasonFunc { get; set; } = _ => null;
        public Func<string?> HostDismReasonFunc { get; set; } = () => null;
        public Func<PowerState> PowerFunc { get; set; } = () => new PowerState(false, false, 100);
        public Func<string, IReadOnlyList<ImageEdition>> EditionsFunc { get; set; } = _ =>
        [
            new ImageEdition(1, "Windows 11 Pro", "Professional", "x64", new Version(10, 0, 26100, 1), 1024)
        ];
        public Func<string, BuildState?> FindResumableFunc { get; set; } = _ => null;
        public Func<BuildRequest, BuildState> StartFunc { get; set; } = r => null!;
        public Func<BuildState, IProgress<BuildProgress>, System.Threading.CancellationToken, BuildResult> RunFunc { get; set; } = null!;
        public int DiscardCalls { get; private set; }

        public IReadOnlyList<ImageEdition> Editions(string source) => EditionsFunc(source);
        public BuildState? FindResumable(string outputIso) => FindResumableFunc(outputIso);
        public BuildState Start(BuildRequest request) => StartFunc(request);
        public BuildResult Run(BuildState state, IProgress<BuildProgress> progress, System.Threading.CancellationToken ct) => RunFunc(state, progress, ct);
        public void Discard(BuildState state) => DiscardCalls++;
        public string? HostDismReason() => HostDismReasonFunc();
        public PowerState Power() => PowerFunc();
        public string? PathsReason(BuildRequest request) => PathsReasonFunc(request);
    }

    private sealed class FakeDialogs : IDialogs
    {
        public bool ConfirmResponse { get; set; } = true;
        public int ConfirmCalls { get; private set; }
        public ConfirmRequest? LastRequest { get; private set; }

        public Task<bool> ConfirmAsync(ConfirmRequest request)
        {
            ConfirmCalls++;
            LastRequest = request;
            return Task.FromResult(ConfirmResponse);
        }

        public Task ShowAsync(string title, string message) => Task.CompletedTask;
    }

    private sealed class DummyScreen(ScreenKey key) : IScreen
    {
        public ScreenKey Key => key;
        public void OnShown(object? argument) { }
    }

    private static (IsoSourceViewModel Vm, Draft Draft, FakeIsoBackend Backend, FakeFilePicker Picker, FakeDialogs Dialogs, IsoBuildIndex Index, string TempDir) CreateVm(Catalog? catalog = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "nitros-isosource-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        cat ??= new Catalog([], new CardMeasurements());
        var draft = new Draft(cat, DeviceProfile.Desktop);
        var nav = new Navigator(new RunGate());
        nav.Register(ScreenKeys.Cards, () => new DummyScreen(ScreenKeys.Cards));
        nav.Register(ScreenKeys.Working, () => new DummyScreen(ScreenKeys.Working));
        var backend = new FakeIsoBackend();
        var picker = new FakeFilePicker();
        var dialogs = new FakeDialogs();
        var session = new FakeSession(elevated: true);
        var index = new IsoBuildIndex(tempDir, _ => FolderState.Missing, p => Directory.CreateDirectory(p), session);
        var reg = new FakeRegistry();

        var vm = new IsoSourceViewModel(nav, draft, backend, picker, dialogs, index, reg);
        return (vm, draft, backend, picker, dialogs, index, tempDir);
    }
    private static Catalog? cat;

    [Fact]
    public async Task IsoSource_ContinueNeedsBothPathsAndUsableEdition()
    {
        var (vm, _, _, picker, _, _, dir) = CreateVm();
        try
        {
            vm.OnShown(null);
            Assert.False(vm.Continue.CanExecute(null));

            picker.OpenPath = "C:\\test\\win11.iso";
            await vm.PickSource.ExecuteAsync(null);
            Assert.False(vm.Continue.CanExecute(null));

            picker.SavePath = "C:\\test\\out.iso";
            await vm.PickOutput.ExecuteAsync(null);
            Assert.True(vm.Continue.CanExecute(null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoSource_ArmEditionNotUsableWithReason()
    {
        var (vm, _, backend, picker, _, _, dir) = CreateVm();
        try
        {
            backend.EditionsFunc = _ =>
            [
                new ImageEdition(1, "Windows 11 Home", "Core", "arm64", new Version(10, 0, 26100, 1), 1024)
            ];

            picker.OpenPath = "C:\\test\\win11-arm.iso";
            await vm.PickSource.ExecuteAsync(null);

            Assert.Single(vm.Editions);
            var choice = vm.Editions[0];
            Assert.False(choice.Usable);
            Assert.Contains("ARM64", choice.Why);
            Assert.Null(vm.Chosen);
            Assert.False(vm.Continue.CanExecute(null));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoSource_SetsDraftIsoWithEdition()
    {
        var (vm, draft, _, picker, _, _, dir) = CreateVm();
        try
        {
            picker.OpenPath = "C:\\test\\win11.iso";
            await vm.PickSource.ExecuteAsync(null);

            picker.SavePath = "C:\\test\\out.iso";
            await vm.PickOutput.ExecuteAsync(null);

            Assert.True(vm.Continue.CanExecute(null));
            vm.Continue.Execute(null);

            Assert.NotNull(draft.Iso);
            Assert.Equal("C:\\test\\win11.iso", draft.Iso.SourceIso);
            Assert.Equal("C:\\test\\out.iso", draft.Iso.OutputIso);
            Assert.Equal(1, draft.Iso.EditionIndex);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsoSource_ShowsResumeAndThrowAway()
    {
        var (vm, _, _, _, _, index, dir) = CreateVm();
        try
        {
            index.Set(new IsoBuildRecord("C:\\test\\out.iso", "C:\\work", "123", CardId.Gamer, ["sw1"]));

            vm.OnShown(null);

            Assert.NotNull(vm.ResumeText);
            Assert.Contains("Gamer", vm.ResumeText);
            Assert.Contains("C:\\test\\out.iso", vm.ResumeText);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task IsoSource_ThrowAwayAsksFirst()
    {
        var (vm, _, backend, _, dialogs, index, dir) = CreateVm();
        try
        {
            index.Set(new IsoBuildRecord("C:\\test\\out.iso", "C:\\work", "123", CardId.Gamer, ["sw1"]));
            vm.OnShown(null);

            dialogs.ConfirmResponse = false;
            await vm.ThrowAway.ExecuteAsync(null);

            Assert.Equal(1, dialogs.ConfirmCalls);
            Assert.NotNull(index.Read());
            Assert.Equal(0, backend.DiscardCalls);

            dialogs.ConfirmResponse = true;
            await vm.ThrowAway.ExecuteAsync(null);

            Assert.Equal(2, dialogs.ConfirmCalls);
            Assert.Null(index.Read());
            Assert.Null(vm.ResumeText);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IsoSource_NeverChangesSourceNote()
    {
        var (vm, _, _, _, _, _, dir) = CreateVm();
        try
        {
            Assert.Equal("NitrOS never changes your ISO. It writes a new file.", vm.AlwaysNote);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
