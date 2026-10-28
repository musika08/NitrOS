namespace NitrOS.Image.Tests.Iso;

using System;
using NitrOS.Image.Iso;
using NitrOS.Image.Tests.Fakes;

public sealed class BootOrderFileTests
{
    [Fact]
    public void BootOrder_ListsBootAndEfiThenBootWim()
    {
        var files = new FakeImageFiles();
        const string mediaRoot = "W:\\media";

        files.Files["W:\\media\\boot\\bcd"] = "bcd";
        files.Files["W:\\media\\boot\\boot.sdi"] = "sdi";
        files.Files["W:\\media\\efi\\boot\\bootx64.efi"] = "efi";
        files.Files["W:\\media\\sources\\boot.wim"] = "bootwim";
        files.Files["W:\\media\\sources\\install.wim"] = "installwim";

        var lines = BootOrderFile.Lines(mediaRoot, files);

        // Contains boot and efi relative paths
        Assert.Contains(@"boot\bcd", lines);
        Assert.Contains(@"boot\boot.sdi", lines);
        Assert.Contains(@"efi\boot\bootx64.efi", lines);

        // Ends with sources\boot.wim
        Assert.Equal(@"sources\boot.wim", lines[^1]);

        // install.wim is NOT in boot order
        Assert.DoesNotContain(@"sources\install.wim", lines);
    }
}
