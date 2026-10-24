using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class CardsTableTests
{
    private static readonly CardId[] AllCards =
        [CardId.Recommended, CardId.Gamer, CardId.Streamer, CardId.MaxLean];

    private static Catalog Load()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!;
    }

    private static Selection Pick(CardId card, DeviceProfile? device = null) =>
        Selection.FromCard(Load(), card, TargetMode.TunePc, device ?? DeviceProfile.Desktop);

    // Row: spec line (representative switch id). R/G/S/M = Recommended, Gamer, Streamer, Max Lean.
    // Off = kept or unchanged, On = removed or off, Pending = decided by measurement.
    [Theory]
    [InlineData("clipchamp-remove", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]        // bundled consumer apps
    [InlineData("armoury-crate-remove", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]    // manufacturer add-ons
    [InlineData("edge-remove", CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On)]            // Edge
    [InlineData("onedrive-remove", CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On)]        // OneDrive
    [InlineData("search-indexing-off", CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On)]    // search indexing
    [InlineData("game-recording-background-off", CardDefault.Off, CardDefault.On, CardDefault.Off, CardDefault.On)] // Game Bar background recording
    [InlineData("camera-remove", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]        // camera and capture parts
    [InlineData("store-remove", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]         // Microsoft Store
    [InlineData("xbox-app-remove", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]      // Xbox app
    [InlineData("windows-update-auto-off", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)] // Windows Update
    [InlineData("defender-remove", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]      // Defender
    [InlineData("memory-integrity-off", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)] // Memory Integrity
    [InlineData("svchost-grouping-on", CardDefault.Off, CardDefault.Pending, CardDefault.Pending, CardDefault.On)] // fewer service processes
    [InlineData("amd-panel-background-off", CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On)] // vendor GPU panel
    [InlineData("virtualization-off", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]   // Virtual Machine Platform, Hyper-V
    [InlineData("recovery-remove", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]      // recovery tools
    [InlineData("printing-off", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.On)]         // printing
    [InlineData("bluetooth-off", CardDefault.Off, CardDefault.Off, CardDefault.Off, CardDefault.Off)]       // Bluetooth kept
    [InlineData("high-performance-plan", CardDefault.Off, CardDefault.On, CardDefault.On, CardDefault.On)]  // power plan
    [InlineData("copilot-remove", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]          // Windows AI parts
    [InlineData("diagtrack-off", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]           // privacy, ads, tips
    [InlineData("fax-off", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]                 // idle background services
    [InlineData("background-apps-off", CardDefault.On, CardDefault.On, CardDefault.On, CardDefault.On)]     // start-up cleanup, background apps
    public void CardsTable_MatchesSpec(string id, CardDefault recommended, CardDefault gamer,
        CardDefault streamer, CardDefault maxLean)
    {
        var cards = Load().Get(id).Cards;
        Assert.Equal(recommended, cards.Recommended);
        Assert.Equal(gamer, cards.Gamer);
        Assert.Equal(streamer, cards.Streamer);
        Assert.Equal(maxLean, cards.MaxLean);
    }

    [Fact]
    public void Recommended_KeepsWhatTheSpecKeeps()
    {
        var on = Pick(CardId.Recommended).On;
        var kept = new[]
        {
            "edge-remove", "onedrive-remove", "search-indexing-off", "store-remove",
            "xbox-app-remove", "windows-update-auto-off", "defender-remove", "printing-off", "bluetooth-off",
        };
        foreach (var id in kept)
            Assert.DoesNotContain(id, on);
    }

    [Theory]
    [InlineData(CardId.Gamer)]
    [InlineData(CardId.Streamer)]
    public void GamerAndStreamer_LeaveAntiCheatPartsAlone(CardId card)
    {
        var selection = Pick(card);
        Assert.False(selection.IsOn("memory-integrity-off"));
        Assert.False(selection.IsOn("virtualization-off"));
    }

    [Fact]
    public void RemovingStoreWhileKeepingXbox_Warns()
    {
        var gamer = Pick(CardId.Gamer);
        var result = gamer.Toggle("store-remove");
        Assert.True(result.Changed);
        var warnings = WarningEngine.For(result.Selection);
        Assert.Contains(warnings, w => w.Kind == WarningKind.Dependency && w.RelatedId == "xbox-app-remove");
    }

    [Theory]
    [InlineData(CardId.Recommended)]
    [InlineData(CardId.Gamer)]
    [InlineData(CardId.Streamer)]
    [InlineData(CardId.MaxLean)]
    public void PowerPlan_SkippedOnBattery(CardId card)
    {
        var selection = Pick(card, new DeviceProfile(true, false, false));
        Assert.False(selection.IsOn("high-performance-plan"));
    }

    [Fact]
    public void MaxLean_SaysWhatStopsWorking()
    {
        var catalog = Load();
        var selection = Selection.FromCard(catalog, CardId.MaxLean, TargetMode.TunePc, DeviceProfile.Desktop);
        var messages = WarningEngine.For(selection)
            .Where(w => w.Kind == WarningKind.Breaks).Select(w => w.Message).ToList();
        foreach (var sentence in catalog.Get("defender-remove").Breaks)
            Assert.Contains(sentence, messages);
        foreach (var sentence in catalog.Get("memory-integrity-off").Breaks)
            Assert.Contains(sentence, messages);
    }

    [Theory]
    [InlineData(CardId.Recommended)]
    [InlineData(CardId.Gamer)]
    [InlineData(CardId.Streamer)]
    [InlineData(CardId.MaxLean)]
    public void EveryCard_TurnsSomethingOn(CardId card)
    {
        Assert.NotEmpty(Pick(card).On);
    }
}
