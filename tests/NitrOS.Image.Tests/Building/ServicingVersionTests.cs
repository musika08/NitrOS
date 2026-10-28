namespace NitrOS.Image.Tests.Building;

using System;
using NitrOS.Image.Building;
using NitrOS.Image.Tests.Fakes;

public sealed class ServicingVersionTests
{
    [Fact]
    public void Build_RefusesOlderHostDismAfterMountAndDiscards()
    {
        var hostDism = new Version(10, 0, 26100);
        var imageServicing = new Version(10, 0, 27100);

        var err = ServicingVersion.Check(hostDism, imageServicing);
        Assert.NotNull(err);
        Assert.Contains("DISM 27100 or later", err);
        Assert.Contains("26100", err);

        var okErr = ServicingVersion.Check(new Version(10, 0, 26100), new Version(10, 0, 26100));
        Assert.Null(okErr);
    }

    [Fact]
    public void ServicingVersion_FromMountFindsHighestVersion()
    {
        var files = new FakeImageFiles();
        files.Files["M:\\mount\\Windows\\servicing\\Version\\10.0.26100.1\\file"] = "x";
        files.Files["M:\\mount\\Windows\\servicing\\Version\\10.0.26100.9703\\file"] = "x";

        var v = ServicingVersion.FromMount("M:\\mount", files);
        Assert.NotNull(v);
        Assert.Equal(new Version(10, 0, 26100, 9703), v);
    }
}
