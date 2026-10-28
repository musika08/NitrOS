namespace NitrOS.Updates.Windows;

using NitrOS.Live.Systems;
using NitrOS.Live.Windows;
using NitrOS.Updates.Reading;

public sealed class WinIdentity : IIdentitySource
{
    private readonly IRegistry _registry;

    public WinIdentity(IRegistry? registry = null)
    {
        _registry = registry ?? new WinRegistry();
    }

    private string? ReadString(string name)
    {
        var val = _registry.GetValue(
            LiveHive.LocalMachine,
            @"HARDWARE\DESCRIPTION\System\BIOS",
            name,
            RegView.Default);
        return val?.Data;
    }

    public string Manufacturer => ReadString("SystemManufacturer") ?? "";
    public string ProductName => ReadString("SystemProductName") ?? "";
    public string? BaseBoardProduct => ReadString("BaseBoardProduct");
    public string? BiosVersion => ReadString("BIOSVersion");
    public string? BiosReleaseDate => ReadString("BIOSReleaseDate");
}
