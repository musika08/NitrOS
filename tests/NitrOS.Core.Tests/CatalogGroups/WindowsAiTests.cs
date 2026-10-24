using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class WindowsAiTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "windows-ai");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void WindowsAi_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "copilot-remove", "recall-off", "ai-fabric-off", "click-to-do-off", "ai-agents-off",
            "ai-helper-apps-remove", "edge-ai-off", "paint-ai-off", "notepad-ai-off",
            "office-actions-server-remove",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void WindowsAi_EdgeAiBreaksOnePerSentence()
    {
        Assert.Equal(2, Sw("edge-ai-off").Breaks.Count);
    }

    [Fact]
    public void WindowsAi_SpotCheck()
    {
        var sw = Sw("copilot-remove");
        var expected = new SwitchAction[]
        {
            new AppxRemove("Microsoft.Copilot"),
            new RegistrySet("HKCU", @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", RegistryKind.Dword, "1"),
            new RegistrySet("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", RegistryKind.Dword, "0"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }
}
