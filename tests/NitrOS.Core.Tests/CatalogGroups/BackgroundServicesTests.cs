using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class BackgroundServicesTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "background-services");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void BackgroundServices_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "svchost-grouping-on", "fax-off", "remote-registry-off", "maps-updates-off",
            "phone-service-off", "connected-devices-off", "geolocation-off", "insider-service-off",
            "retail-demo-off", "wap-push-off", "media-sharing-off", "offline-files-off",
            "storage-service-manual", "remote-desktop-off", "compatibility-assistant-off",
            "telemetry-tasks-off", "power-diagnostics-task-off", "delivery-optimization-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void BackgroundServices_MultiSentenceBreaksOnePerSentence()
    {
        Assert.Equal(2, Sw("svchost-grouping-on").Breaks.Count);
        Assert.Equal(2, Sw("geolocation-off").Breaks.Count);
        Assert.Single(Sw("fax-off").Breaks);
    }

    [Fact]
    public void BackgroundServices_SpotCheck()
    {
        var sw = Sw("svchost-grouping-on");
        var expected = new SwitchAction[]
        {
            new RegistrySet("HKLM", @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB", RegistryKind.Dword, "4294967295"),
        };
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
        Assert.Equal(CardDefault.Off, sw.Cards.Recommended);
        Assert.Equal(CardDefault.Pending, sw.Cards.Gamer);
        Assert.Equal(CardDefault.Pending, sw.Cards.Streamer);
        Assert.Equal(CardDefault.On, sw.Cards.MaxLean);
    }

    [Fact]
    public void BackgroundServices_TelemetryTasksHasNineTasks()
    {
        var expected = new SwitchAction[]
        {
            new TaskDisable(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser Exp"),
            new TaskDisable(@"\Microsoft\Windows\Application Experience\StartupAppTask"),
            new TaskDisable(@"\Microsoft\Windows\Application Experience\MareBackup"),
            new TaskDisable(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
            new TaskDisable(@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"),
            new TaskDisable(@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"),
            new TaskDisable(@"\Microsoft\Windows\Feedback\Siuf\DmClient"),
            new TaskDisable(@"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"),
            new TaskDisable(@"\Microsoft\Windows\PI\Sqm-Tasks"),
        };
        Assert.Equal(expected, Sw("telemetry-tasks-off").Live);
        Assert.Equal(expected, Sw("telemetry-tasks-off").Image);
    }

    [Fact]
    public void BackgroundServices_MapsAndGeolocationActions()
    {
        var maps = new SwitchAction[]
        {
            new ServiceStart("MapsBroker", ServiceStartType.Disabled),
            new TaskDisable(@"\Microsoft\Windows\Maps\MapsUpdateTask"),
            new RegistrySet("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\Maps", "AutoDownloadAndUpdateMapData", RegistryKind.Dword, "0"),
        };
        Assert.Equal(maps, Sw("maps-updates-off").Live);
        var geo = new SwitchAction[]
        {
            new ServiceStart("lfsvc", ServiceStartType.Disabled),
            new RegistrySet("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", RegistryKind.String, "Deny"),
        };
        Assert.Equal(geo, Sw("geolocation-off").Live);
    }

    [Fact]
    public void BackgroundServices_ManualStartRowsAndDeliveryOptimization()
    {
        Assert.Equal(new SwitchAction[]
        {
            new ServiceStart("CDPSvc", ServiceStartType.Manual),
            new ServiceStart("CDPUserSvc", ServiceStartType.Manual),
        }, Sw("connected-devices-off").Live);
        Assert.Equal(new SwitchAction[] { new ServiceStart("StorSvc", ServiceStartType.Manual) },
            Sw("storage-service-manual").Live);
        Assert.Equal(new SwitchAction[]
        {
            new ServiceStart("TermService", ServiceStartType.Manual),
            new ServiceStart("SessionEnv", ServiceStartType.Manual),
            new ServiceStart("UmRdpService", ServiceStartType.Manual),
        }, Sw("remote-desktop-off").Live);
        Assert.Equal(new SwitchAction[]
        {
            new RegistrySet("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", RegistryKind.Dword, "99"),
        }, Sw("delivery-optimization-off").Live);
    }

    [Fact]
    public void BackgroundServices_CompatibilityAssistantCardsOnlyMaxLean()
    {
        var c = Sw("compatibility-assistant-off").Cards;
        Assert.Equal(CardDefault.Off, c.Recommended);
        Assert.Equal(CardDefault.Off, c.Gamer);
        Assert.Equal(CardDefault.Off, c.Streamer);
        Assert.Equal(CardDefault.On, c.MaxLean);
    }
}
