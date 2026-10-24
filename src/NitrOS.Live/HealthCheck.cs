namespace NitrOS.Live;

public interface IHealthProbe
{
    bool IsProcessRunning(string processName);
    bool IsServiceRunning(string serviceName);
    bool IsPackageInstalled(string packageName);
    /// <summary>The response body, or null when the request failed.</summary>
    string? HttpGet(string url);
}

public record HealthItem(string Name, bool Ok, string? LikelySwitchId, string? Detail = null);

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
            Check("Start menu", StartSwitch, () => probe.IsProcessRunning("StartMenuExperienceHost")),
            Check("Internet", null, () => probe.HttpGet(ConnectTestUrl)?.Trim() == ConnectTestBody),
            Check("Sound", null, () => probe.IsServiceRunning("Audiosrv")),
        };

        if (!appliedSwitchIds.Contains(BluetoothSwitch))
            items.Add(Check("Bluetooth", BluetoothSwitch, () => probe.IsServiceRunning("bthserv")));

        if (!appliedSwitchIds.Contains(StoreSwitch))
            items.Add(Check("Microsoft Store", StoreSwitch, () => probe.IsPackageInstalled("Microsoft.WindowsStore")));

        var keepXboxApp = !appliedSwitchIds.Contains(XboxAppSwitch);
        var keepGamingServices = !appliedSwitchIds.Contains(GamingServicesSwitch);
        if (keepXboxApp || keepGamingServices)
        {
            // Name the switch whose part actually failed.
            var app = keepXboxApp ? Check("Game Pass", XboxAppSwitch, () => probe.IsPackageInstalled("Microsoft.GamingApp")) : null;
            var svc = keepGamingServices ? Check("Game Pass", GamingServicesSwitch, () => probe.IsServiceRunning("GamingServices")) : null;
            items.Add(app is { Ok: false } ? app : svc is { Ok: false } ? svc : new HealthItem("Game Pass", true, null));
        }

        return new HealthReport(items);
    }

    // A probe that throws fails only its own item, with the message kept as Detail.
    private static HealthItem Check(string name, string? likelySwitch, Func<bool> test)
    {
        try { return Item(name, test(), likelySwitch); }
        catch (Exception e) { return new HealthItem(name, false, likelySwitch, e.Message); }
    }

    private static HealthItem Item(string name, bool ok, string? likely) => new(name, ok, ok ? null : likely);
}
