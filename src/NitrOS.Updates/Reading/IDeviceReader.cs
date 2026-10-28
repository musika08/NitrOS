namespace NitrOS.Updates.Reading;

using System.Collections.Generic;
using NitrOS.Updates.Model;

public interface IIdentitySource
{
    string Manufacturer { get; }
    string ProductName { get; }
    string? BaseBoardProduct { get; }
    string? BiosVersion { get; }
    string? BiosReleaseDate { get; }
}

public interface IInstalledDevices
{
    IReadOnlyList<InstalledDevice> Present();
}

public interface IFirmwareTable
{
    IReadOnlyList<FirmwareResource> Read();
}

public interface IDeviceReader
{
    DeviceInfo Read();
}
