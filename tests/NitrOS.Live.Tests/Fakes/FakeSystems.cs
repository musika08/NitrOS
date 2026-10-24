using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

/// <summary>Bundles one of each fake so tests can reach their public state.</summary>
public sealed class FakeSystems
{
    public FakeRegistry Registry { get; } = new();
    public FakeServices Services { get; } = new();
    public FakeTasks Tasks { get; } = new();
    public FakePower Power { get; } = new();
    public FakeAppx Appx { get; } = new();
    public FakeDism Dism { get; } = new();
    public FakeUninstaller Uninstaller { get; } = new();
    public FakeFiles Files { get; } = new();
    public FakeScripts Scripts { get; } = new();
    public FakeRestorePoint RestorePoint { get; } = new();

    public LiveSystems ToLive() => new(Registry, Services, Tasks, Power, Appx, Dism, Uninstaller, Files, Scripts, RestorePoint);
}
