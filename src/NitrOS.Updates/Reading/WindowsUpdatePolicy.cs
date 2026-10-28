namespace NitrOS.Updates.Reading;

using NitrOS.Live.Systems;

public sealed class WindowsUpdatePolicy(IRegistry registry)
{
    public bool DriversExcluded()
    {
        var val = registry.GetValue(
            LiveHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
            "ExcludeWUDriversInQualityUpdate",
            RegView.Default);

        if (val is null) return false;
        return val.Data == "1";
    }
}
