using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class EdgeOnedriveTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "edge-onedrive");
    }

    [Fact]
    public void EdgeOnedrive_HasExactlyTheVerifiedIds()
    {
        var expected = new[] { "edge-remove", "onedrive-remove" };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void EdgeOnedrive_SpotCheck()
    {
        var sw = Group().Switches.Single(s => s.Id == "edge-remove");
        const string uninstall = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\";
        Assert.Equal(new SwitchAction[]
        {
            new ProgramUninstall("Microsoft Edge", "Microsoft Corporation", "--force-uninstall"),
            new AppxRemove("Microsoft.MicrosoftEdge.Stable"),
            new RegistryDelete("HKLM", uninstall + "Microsoft Edge"),
            new RegistryDelete("HKLM", uninstall + "Microsoft Edge Update"),
        }, sw.Live);
        Assert.Equal(new SwitchAction[]
        {
            new FileRemove(@"%ProgramFiles(x86)%\Microsoft\Edge"),
            new FileRemove(@"%ProgramFiles(x86)%\Microsoft\EdgeUpdate"),
            new FileRemove(@"%ProgramFiles(x86)%\Microsoft\EdgeCore"),
            new RegistryDelete("HKLM", uninstall + "Microsoft Edge"),
            new RegistryDelete("HKLM", uninstall + "Microsoft Edge Update"),
        }, sw.Image);
    }
}
