namespace NitrOS.Updates.Tests.Fakes;

using System;
using System.Collections.Generic;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;

public sealed class FakeIdentitySource : IIdentitySource
{
    public string Manufacturer { get; set; } = "ASUSTeK COMPUTER INC.";
    public string ProductName { get; set; } = "ROG Xbox Ally X RC73XA_RC73XA";
    public string? BaseBoardProduct { get; set; } = "RC73XA";
    public string? BiosVersion { get; set; } = "RC73XA.319";
    public string? BiosReleaseDate { get; set; } = "09/23/2026";
}

public sealed class FakeInstalledDevices : IInstalledDevices
{
    public List<InstalledDevice> Devices { get; set; } = [];
    public Func<IReadOnlyList<InstalledDevice>>? PresentFunc { get; set; }

    public IReadOnlyList<InstalledDevice> Present()
    {
        if (PresentFunc != null) return PresentFunc();
        return Devices;
    }
}

public sealed class FakeFirmwareTable : IFirmwareTable
{
    public List<FirmwareResource> Resources { get; set; } = [];
    public Func<IReadOnlyList<FirmwareResource>>? ReadFunc { get; set; }

    public IReadOnlyList<FirmwareResource> Read()
    {
        if (ReadFunc != null) return ReadFunc();
        return Resources;
    }
}

public sealed class FakeDeviceReader(DeviceInfo info) : IDeviceReader
{
    public DeviceInfo Read() => info;
}
