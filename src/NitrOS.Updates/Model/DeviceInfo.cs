namespace NitrOS.Updates.Model;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed record InstalledDevice(
    string InstanceId,
    string Name,
    string? DeviceClass,
    IReadOnlyList<string> HardwareIds,
    string? DriverVersion,
    DateOnly? DriverDate,
    string? DriverProvider);

public sealed record FirmwareResource(
    Guid Id,
    int Type,
    uint Version,
    uint LastAttemptVersion,
    uint LastAttemptStatus,
    string? PackageVersion,
    string? Name);

public sealed record DeviceInfo(
    MakerId Maker,
    string Manufacturer,
    string Model,
    string? ModelCode,
    string? BiosVersion,
    DateOnly? BiosDate,
    IReadOnlyList<InstalledDevice> Devices,
    IReadOnlyList<FirmwareResource> Firmware)
{
    public IReadOnlyList<InstalledDevice> Gpus => Devices
        .Where(d => string.Equals(d.DeviceClass, "Display", StringComparison.OrdinalIgnoreCase))
        .ToList();
}
