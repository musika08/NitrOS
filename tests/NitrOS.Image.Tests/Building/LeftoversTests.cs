namespace NitrOS.Image.Tests.Building;

using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;

public sealed class LeftoversTests
{
    [Fact]
    public void Leftovers_UnloadsHivesBeforeAnyDismCall()
    {
        var fakes = new FakeImageSystems();
        fakes.Registry.Load("C:\\SOFTWARE", "NitrOS.Image.1.SOFTWARE");

        var report = Leftovers.Clear(fakes.Build(), null, null);
        Assert.Single(report.UnloadedHives);
        Assert.Equal("NitrOS.Image.1.SOFTWARE", report.UnloadedHives[0]);
    }

    [Fact]
    public void Leftovers_DiscardsInvalidMountAndRestartsFromExport()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Mounts.Add(new MountedImage("C:\\NitrOS-work-old\\mount", "C:\\NitrOS-work-old\\install.wim", 1, MountStatus.Invalid));

        var report = Leftovers.Clear(fakes.Build(), null, null);
        Assert.Single(report.DiscardedMounts);
        Assert.True(report.CleanedMountpoints);
    }

    [Fact]
    public void Leftovers_RemountsNeedsRemount()
    {
        var fakes = new FakeImageSystems();
        var mountDir = "C:\\NitrOS-work-1234\\mount";
        fakes.Dism.Mounts.Add(new MountedImage(mountDir, "C:\\NitrOS-work-1234\\install.wim", 1, MountStatus.NeedsRemount));

        var state = new BuildState(
            "1234",
            new BuildRequest("src", "out", null, [], null),
            "hash",
            new SourceFingerprint(10, System.DateTimeOffset.UtcNow),
            1,
            "Pro",
            BuildStep.ApplyDism,
            [],
            "C:\\NitrOS-work-1234",
            System.DateTimeOffset.UtcNow,
            System.DateTimeOffset.UtcNow);

        var report = Leftovers.Clear(fakes.Build(), state, null);
        Assert.Equal(MountStatus.NeedsRemount, report.ResumeMount);
        Assert.Empty(report.DiscardedMounts);
    }

    [Fact]
    public void Leftovers_NeverTouchesOtherToolsMounts()
    {
        var fakes = new FakeImageSystems();
        fakes.Dism.Mounts.Add(new MountedImage("C:\\OtherTool\\mount", "C:\\OtherTool\\install.wim", 1, MountStatus.Ok));

        var report = Leftovers.Clear(fakes.Build(), null, null);
        Assert.Empty(report.DiscardedMounts);
        Assert.Single(fakes.Dism.Mounts);
    }
}
