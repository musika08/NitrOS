namespace NitrOS.Updates.Tests.Reading;

using System;
using System.Collections.Generic;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;
using NitrOS.Updates.Tests.Fakes;

public sealed class DeviceReaderTests
{
    [Fact]
    public void DeviceReader_ModelCodeFromBaseBoard()
    {
        var identity = new FakeIdentitySource
        {
            BaseBoardProduct = "RC73XA",
            ProductName = "ROG Xbox Ally X"
        };
        var reader = new DeviceReader(identity, new FakeInstalledDevices(), new FakeFirmwareTable());
        var info = reader.Read();

        Assert.Equal("RC73XA", info.ModelCode);
    }

    [Fact]
    public void DeviceReader_ModelCodeFromProductName()
    {
        var identity = new FakeIdentitySource
        {
            BaseBoardProduct = null,
            ProductName = "ROG Xbox Ally X RC73XA_RC73XA"
        };
        var reader = new DeviceReader(identity, new FakeInstalledDevices(), new FakeFirmwareTable());
        var info = reader.Read();

        Assert.Equal("RC73XA", info.ModelCode);
    }

    [Fact]
    public void DeviceReader_NoModelCode()
    {
        var identity = new FakeIdentitySource
        {
            BaseBoardProduct = "Custom-Board-1!",
            ProductName = "Desktop PC"
        };
        var reader = new DeviceReader(identity, new FakeInstalledDevices(), new FakeFirmwareTable());
        var info = reader.Read();

        Assert.Null(info.ModelCode);
    }

    [Fact]
    public void DeviceReader_ReaderFailureGivesEmptyPart()
    {
        var identity = new FakeIdentitySource();
        var devices = new FakeInstalledDevices
        {
            PresentFunc = () => throw new InvalidOperationException("Pnp failed")
        };
        var firmware = new FakeFirmwareTable
        {
            Resources = [new FirmwareResource(Guid.NewGuid(), 1, 100, 100, 0, "1.0", "System FW")]
        };

        var reader = new DeviceReader(identity, devices, firmware);
        var info = reader.Read();

        Assert.Empty(info.Devices);
        Assert.Single(info.Firmware);
        Assert.Equal("ASUSTeK COMPUTER INC.", info.Manufacturer);
    }

    [Fact]
    public void DeviceReader_GpusAreDisplayClass()
    {
        var identity = new FakeIdentitySource();
        var devices = new FakeInstalledDevices
        {
            Devices =
            [
                new InstalledDevice("DEV1", "AMD Radeon 890M", "Display", ["PCI\\VEN_1002&DEV_150E"], "32.0.1", null, "AMD"),
                new InstalledDevice("DEV2", "Realtek Audio", "MEDIA", ["HDAUDIO\\..."], "6.0.1", null, "Realtek")
            ]
        };

        var reader = new DeviceReader(identity, devices, new FakeFirmwareTable());
        var info = reader.Read();

        Assert.Single(info.Gpus);
        Assert.Equal("AMD Radeon 890M", info.Gpus[0].Name);
    }
}
