using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Tests.TestSupport;

namespace NitrOS.Core.Tests.CatalogGroups;

public class PrivacyAdsTests
{
    private static GroupFile Group()
    {
        var result = CatalogLoader.Load(RepoPaths.CatalogDir());
        Assert.True(result.Ok, string.Join(Environment.NewLine,
            result.Errors.Select(e => $"{e.File}:{e.Line} [{e.SwitchId}] {e.Message}")));
        return result.Catalog!.Groups.Single(g => g.Group == "privacy-ads");
    }

    private static SwitchDef Sw(string id) => Group().Switches.Single(s => s.Id == id);

    [Fact]
    public void PrivacyAds_HasExactlyTheVerifiedIds()
    {
        var expected = new[]
        {
            "consumer-features-off", "diagtrack-off", "advertising-id-off",
            "windows-ads-off", "start-recommendations-off", "tailored-experiences-off",
            "telemetry-required-only", "feedback-prompts-off", "activity-history-off",
            "typing-speech-data-off", "search-web-results-off", "app-launch-tracking-off",
            "edge-tracking-off", "onedrive-backup-prompt-off",
        };
        var actual = Group().Switches.Select(s => s.Id);
        Assert.Equal(expected.OrderBy(x => x, StringComparer.Ordinal),
            actual.OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void PrivacyAds_SingleSentenceBreaksHaveOneItem()
    {
        Assert.Single(Sw("windows-ads-off").Breaks);
        Assert.Single(Sw("start-recommendations-off").Breaks);
        Assert.Single(Sw("search-web-results-off").Breaks);
        Assert.Single(Sw("app-launch-tracking-off").Breaks);
        Assert.Single(Sw("edge-tracking-off").Breaks);
    }

    [Fact]
    public void PrivacyAds_TypingSpeechBreaksOnlyWhatStops()
    {
        Assert.Equal(new[] { "Online voice typing (cloud dictation) stops." }, Sw("typing-speech-data-off").Breaks);
    }

    [Fact]
    public void PrivacyAds_SpotCheck()
    {
        var sw = Sw("windows-ads-off");
        const string cdm = "Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager";
        var names = new[]
        {
            "ContentDeliveryAllowed", "SubscribedContentEnabled", "FeatureManagementEnabled",
            "SoftLandingEnabled", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled",
            "PreInstalledAppsEverEnabled", "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled",
            "RotatingLockScreenOverlayEnabled", "SubscribedContent-310093Enabled",
            "SubscribedContent-338387Enabled", "SubscribedContent-338388Enabled",
            "SubscribedContent-338389Enabled", "SubscribedContent-338393Enabled",
            "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled",
        };
        var expected = names.Select(n => (SwitchAction)new RegistrySet("HKCU", cdm, n, RegistryKind.Dword, "0"))
            .Concat(new SwitchAction[]
            {
                new RegistrySet("HKLM", "SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableConsumerAccountStateContent", RegistryKind.Dword, "1"),
                new RegistrySet("HKLM", "SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent", "DisableCloudOptimizedContent", RegistryKind.Dword, "1"),
                new RegistrySet("HKLM", "SOFTWARE\\Policies\\Microsoft\\PushToInstall", "DisablePushToInstall", RegistryKind.Dword, "1"),
                new RegistrySet("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion\\SystemSettings\\AccountNotifications", "EnableAccountNotifications", RegistryKind.Dword, "0"),
                new RegistrySet("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "ShowSyncProviderNotifications", RegistryKind.Dword, "0"),
                new RegistrySet("HKCU", "Software\\Microsoft\\Windows\\CurrentVersion\\UserProfileEngagement", "ScoobeSystemSettingEnabled", RegistryKind.Dword, "0"),
            }).ToArray();
        Assert.Equal(expected, sw.Live);
        Assert.Equal(expected, sw.Image);
    }

    [Fact]
    public void PrivacyAds_ExistingThreeUnchanged()
    {
        Assert.Equal(
            new SwitchAction[] { new ServiceStart("DiagTrack", ServiceStartType.Disabled) },
            Sw("diagtrack-off").Live);
    }
}
