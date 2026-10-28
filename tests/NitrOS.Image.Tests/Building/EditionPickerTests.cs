namespace NitrOS.Image.Tests.Building;

using System;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;

public sealed class EditionPickerTests
{
    [Fact]
    public void Build_PicksProfessionalByDefault()
    {
        var e1 = new ImageEdition(1, "Windows 11 Home", "Core", "x64", new Version(10, 0, 26100), 1000);
        var e2 = new ImageEdition(2, "Windows 11 Pro", "Professional", "x64", new Version(10, 0, 26100), 1000);
        var e3 = new ImageEdition(3, "Windows 11 Enterprise", "Enterprise", "x64", new Version(10, 0, 26100), 1000);

        var picked = EditionPicker.Pick([e1, e2, e3], requested: null);
        Assert.NotNull(picked);
        Assert.Equal(2, picked.Index);

        // Requested index wins
        var pickedRequested = EditionPicker.Pick([e1, e2, e3], requested: 3);
        Assert.NotNull(pickedRequested);
        Assert.Equal(3, pickedRequested.Index);
    }

    [Fact]
    public void Build_RefusesArm64AndOld()
    {
        var arm64 = new ImageEdition(1, "Windows 11 Pro", "Professional", "arm64", new Version(10, 0, 26100), 1000);
        var errArm = EditionPicker.Refusal(arm64);
        Assert.NotNull(errArm);
        Assert.Contains("x64", errArm);

        var old = new ImageEdition(1, "Windows 10 Pro", "Professional", "x64", new Version(10, 0, 22621), 1000);
        var errOld = EditionPicker.Refusal(old);
        Assert.NotNull(errOld);
        Assert.Contains("Windows 11 24H2 or newer", errOld);

        var ok = new ImageEdition(1, "Windows 11 Pro", "Professional", "x64", new Version(10, 0, 26100), 1000);
        Assert.Null(EditionPicker.Refusal(ok));
    }
}
