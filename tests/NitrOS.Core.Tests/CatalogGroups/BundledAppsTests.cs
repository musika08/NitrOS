using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class BundledAppsTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "bundled-apps");
    }

    [Fact]
    public void BundledApps_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "clipchamp-remove", "news-remove", "weather-remove", "solitaire-remove", "teams-remove",
            "outlook-new-remove", "phone-link-remove", "feedback-hub-remove", "get-help-remove",
            "todo-remove", "widgets-remove", "office-hub-remove", "bing-search-remove",
            "movies-tv-remove", "media-player-remove", "sound-recorder-remove", "clock-remove",
            "sticky-notes-remove", "quick-assist-remove", "dev-home-remove", "power-automate-remove",
            "xbox-insider-remove", "edge-game-assist-remove", "calculator-remove", "notepad-remove",
            "paint-remove", "photos-remove", "terminal-remove", "camera-remove",
            "snipping-tool-remove", "app-installer-remove",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void BundledApps_SpotCheck()
    {
        var sw = Group().Switches.Single(s => s.Id == "clipchamp-remove");
        var expected = new SwitchAction[] { new AppxRemove("Clipchamp.Clipchamp") };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }

    [Fact]
    public void BundledApps_EdgeGameAssistRequiresEdgeRemove()
    {
        var sw = Group().Switches.Single(s => s.Id == "edge-game-assist-remove");
        Assert.Equal(new[] { "edge-remove", "game-bar-remove" }, sw.Requires);
    }

    [Fact]
    public void BundledApps_PhoneLinkRequiresItsServices()
    {
        var sw = Group().Switches.Single(s => s.Id == "phone-link-remove");
        Assert.Equal(new[] { "connected-devices-off", "phone-service-off" }, sw.Requires);
    }

    [Fact]
    public void BundledApps_ClipchampBreaksDoesNotPromiseTheStore()
    {
        var sw = Group().Switches.Single(s => s.Id == "clipchamp-remove");
        Assert.Equal(new[] { "Clipchamp is gone; reinstall it from the Microsoft Store if you keep the Store." }, sw.Breaks);
    }
}
