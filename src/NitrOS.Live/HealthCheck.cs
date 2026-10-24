namespace NitrOS.Live;

public interface IHealthProbe
{
    bool IsProcessRunning(string processName);
    bool IsServiceRunning(string serviceName);
    bool IsPackageInstalled(string packageName);
    /// <summary>The response body, or null when the request failed.</summary>
    string? HttpGet(string url);
}

public record HealthItem(string Name, bool Ok, string? LikelySwitchId);

public record HealthReport(IReadOnlyList<HealthItem> Items)
{
    public bool AllOk => Items.All(i => i.Ok);
}

/// <summary>After-restart check that the basics still work. Items for removed features are skipped.</summary>
public static class HealthCheck
{
    public const string ConnectTestUrl = "http://www.msftconnecttest.com/connecttest.txt";
    public const string ConnectTestBody = "Microsoft Connect Test";

    // Catalog switch ids most likely to have broken each item.
    private const string StartSwitch = "windows-ads-off";
    private const string StoreSwitch = "store-remove";
    private const string XboxAppSwitch = "xbox-app-remove";
    private const string GamingServicesSwitch = "gaming-services-remove";
    private const string BluetoothSwitch = "bluetooth-off";

    /// <param name="appliedSwitchIds">Switch ids that are ON. A kept-feature check runs only when its removal switch is not in the set.</param>
    public static HealthReport Run(IHealthProbe probe, IReadOnlySet<string> appliedSwitchIds)
    {
        var items = new List<HealthItem>
        {
            Item("Start menu", probe.IsProcessRunning("StartMenuExperienceHost"), StartSwitch),
            Item("Internet", IsInternetUp(probe), null),
            Item("Sound", probe.IsServiceRunning("Audiosrv"), null),
        };

        if (!appliedSwitchIds.Contains(BluetoothSwitch))
            items.Add(Item("Bluetooth", probe.IsServiceRunning("bthserv"), BluetoothSwitch));

        if (!appliedSwitchIds.Contains(StoreSwitch))
            items.Add(Item("Microsoft Store", probe.IsPackageInstalled("Microsoft.WindowsStore"), StoreSwitch));

        var keepXboxApp = !appliedSwitchIds.Contains(XboxAppSwitch);
        var keepGamingServices = !appliedSwitchIds.Contains(GamingServicesSwitch);
        if (keepXboxApp || keepGamingServices)
        {
            var appOk = !keepXboxApp || probe.IsPackageInstalled("Microsoft.GamingApp");
            var servicesOk = !keepGamingServices || probe.IsServiceRunning("GamingServices");
            // Name the switch whose part actually failed.
            var likely = !appOk ? XboxAppSwitch : GamingServicesSwitch;
            items.Add(Item("Game Pass", appOk && servicesOk, likely));
        }

        return new HealthReport(items);
    }

    private static bool IsInternetUp(IHealthProbe probe)
    {
        try { return probe.HttpGet(ConnectTestUrl)?.Trim() == ConnectTestBody; }
        catch (Exception) { return false; }
    }

    private static HealthItem Item(string name, bool ok, string? likely) => new(name, ok, ok ? null : likely);
}
