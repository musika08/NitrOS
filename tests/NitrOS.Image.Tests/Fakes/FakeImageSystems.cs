namespace NitrOS.Image.Tests.Fakes;

using NitrOS.Image.Systems;

public sealed class FakeImageSystems
{
    public FakeImageServicing Dism { get; } = new();
    public FakeOfflineRegistry Registry { get; } = new();
    public FakeImageFiles Files { get; } = new();
    public FakeSourceMedia Media { get; } = new();
    public FakeIsoWriter Iso { get; } = new();
    public FakeHostFacts Host { get; } = new();
    public FakePowerSource Power { get; } = new();

    public ImageSystems Build() =>
        new(Dism, Registry, Files, Media, Iso, Host, Power);
}
