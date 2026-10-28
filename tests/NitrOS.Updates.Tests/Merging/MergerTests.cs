using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;
using Xunit;

namespace NitrOS.Updates.Tests.Merging;

public sealed class MergerTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    private static UpdateItem CreateItem(
        string key,
        string name,
        UpdateKind kind,
        string? latestVersion,
        string? installedVersion,
        string sourceId,
        InstallMethod method = InstallMethod.MakerPackage,
        Uri? downloadUrl = null)
    {
        return new UpdateItem(
            key,
            name,
            kind,
            installedVersion,
            latestVersion,
            downloadUrl,
            1024,
            null,
            sourceId,
            sourceId)
        {
            Method = method
        };
    }

    [Fact]
    public void Merge_NewestWinsAndSourceShown()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        var gpuKey = "dev:PCI\\VEN_1002&DEV_15BF";
        var asusItem = CreateItem(
            gpuKey,
            "AMD Graphics Driver",
            UpdateKind.Driver,
            "32.0.32015.5008",
            "32.0.32015.7029",
            "asus",
            InstallMethod.MakerPackage,
            new Uri("https://asus.com/driver.exe"));

        var vendorItem = CreateItem(
            gpuKey,
            "AMD Graphics Driver",
            UpdateKind.Driver,
            "32.0.33000.1",
            "32.0.32015.7029",
            "gpu-vendor",
            InstallMethod.MakerPackage,
            new Uri("https://amd.com/driver.exe"));

        var result = Merger.Merge([asusItem, vendorItem], ally, knownBad, map);

        Assert.Single(result);
        var merged = result[0];
        Assert.Equal("gpu-vendor", merged.Item.SourceId);
        Assert.Equal("32.0.33000.1", merged.Item.LatestVersion);
        Assert.Single(merged.AlsoOfferedBy);
        Assert.Equal("asus", merged.AlsoOfferedBy[0].SourceId);
    }

    [Fact]
    public void Merge_InstalledNewerThanOfferedIsUpToDate()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        var asusItem = CreateItem(
            "dev:PCI\\VEN_1002&DEV_15BF",
            "AMD Graphics Driver",
            UpdateKind.Driver,
            "32.0.32015.5008",
            "32.0.32015.7029",
            "asus",
            InstallMethod.MakerPackage,
            new Uri("https://asus.com/driver.exe"));

        var result = Merger.Merge([asusItem], ally, knownBad, map);
        Assert.Single(result);
        Assert.Equal(ItemState.UpToDate, result[0].State);
    }

    [Fact]
    public void Merge_WindowsUpdateOlderThanInstalledIsUpToDate()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        var wuItem = CreateItem(
            "dev:USB\\VID_0BDA&PID_8153",
            "Realtek - Net - 11.19.20.602",
            UpdateKind.Driver,
            "11.19.20.602",
            "11.19.602.2025",
            "windows-update",
            InstallMethod.WindowsUpdate);

        var result = Merger.Merge([wuItem], ally, knownBad, map);
        Assert.Single(result);
        Assert.Equal(ItemState.UpToDate, result[0].State);
    }

    [Fact]
    public void Merge_WuBiosAlreadyFlashedByMakerToolIsUpToDate()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        // Installed BIOS is 319 (ESRT version 793, package 10.1.2.317)
        // WU offers 10.1.2.319 on fw:{68e0fa36-7c6c-4861-ba1b-d10fbde48a1c}
        // ASUS offers "319" on the same key
        var biosGuid = "68e0fa36-7c6c-4861-ba1b-d10fbde48a1c";
        var wuItem = CreateItem(
            $"fw:{biosGuid}",
            "BIOS Update for Windows",
            UpdateKind.Bios,
            "10.1.2.319",
            "10.1.2.317",
            "windows-update",
            InstallMethod.WindowsUpdate);

        var asusItem = CreateItem(
            $"fw:{biosGuid}",
            "BIOS Update for Windows",
            UpdateKind.Bios,
            "319",
            "319",
            "asus",
            InstallMethod.HandOff,
            new Uri("https://asus.com/bios.exe"));

        var result = Merger.Merge([wuItem, asusItem], ally, knownBad, map);
        Assert.Single(result);
        Assert.Equal(ItemState.UpToDate, result[0].State);
    }

    [Fact]
    public void Merge_PdFirmwareNewVersion()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        // PD installed ESRT is 21 (0x0015 = 21, package 10.1.2.15).
        // Offered is 0020 (0x0020 = 32) from ASUS and 10.1.2.20 from WU.
        var pdGuid = "cbf5a488-f654-3fce-b3fc-8a1d94ead498";
        var wuItem = CreateItem(
            $"fw:{pdGuid}",
            "PD_FW Firmware",
            UpdateKind.Firmware,
            "10.1.2.20",
            "10.1.2.15",
            "windows-update",
            InstallMethod.WindowsUpdate);

        var asusItem = CreateItem(
            $"fw:{pdGuid}",
            "PD_FW Firmware",
            UpdateKind.Firmware,
            "0020",
            "0015",
            "asus",
            InstallMethod.HandOff,
            new Uri("https://asus.com/pd.exe"));

        var result = Merger.Merge([asusItem, wuItem], ally, knownBad, map);
        Assert.Single(result);
        var merged = result[0];
        Assert.Equal(ItemState.NewVersion, merged.State);
        // Tie on ESRT number 32 goes to windows-update
        Assert.Equal("windows-update", merged.Item.SourceId);
    }

    [Fact]
    public void Merge_UnknownInstalledIsCantTell()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        var mcuItem = CreateItem(
            "app:asus-mcu",
            "ROG Ally MCU Tool",
            UpdateKind.Firmware,
            "V326",
            null,
            "asus",
            InstallMethod.HandOff,
            new Uri("https://asus.com/mcu.exe"));

        var result = Merger.Merge([mcuItem], ally, knownBad, map);
        Assert.Single(result);
        Assert.Equal(ItemState.CantTell, result[0].State);
    }

    [Fact]
    public void Merge_KnownBadNewestFallsBackToOlderNewer()
    {
        var ally = LoadAllyFixture();
        var map = FirmwareMap.Shipped();

        var badList = KnownBadList.Parse("""
        {
          "entries": [
            {
              "maker": "Asus",
              "model": "RC73XA",
              "kind": "bios",
              "version": "320",
              "reason": "Test bad bios.",
              "source": "https://example.com"
            }
          ]
        }
        """);

        var biosGuid = "68e0fa36-7c6c-4861-ba1b-d10fbde48a1c";
        var badItem = CreateItem(
            $"fw:{biosGuid}",
            "BIOS Update for Windows",
            UpdateKind.Bios,
            "320",
            "318",
            "asus",
            InstallMethod.HandOff);

        var goodItem = CreateItem(
            $"fw:{biosGuid}",
            "BIOS Update for Windows",
            UpdateKind.Bios,
            "319",
            "318",
            "windows-update",
            InstallMethod.WindowsUpdate);

        // Mutate ally installed bios to 318
        var mutatedFirmware = ally.Firmware.Select(f => f.Type == 1
            ? new FirmwareResource(f.Id, f.Type, 792, f.LastAttemptVersion, f.LastAttemptStatus, "10.1.2.318", f.Name)
            : f).ToList();
        var ally318 = ally with { BiosVersion = "RC73XA.318", Firmware = mutatedFirmware };

        var result = Merger.Merge([badItem, goodItem], ally318, badList, map);
        Assert.Single(result);
        var merged = result[0];
        Assert.Equal("319", merged.Item.LatestVersion);
        Assert.Equal(ItemState.NewVersion, merged.State);
    }

    [Fact]
    public void Merge_AllBlockedIsBlockedWithReason()
    {
        var ally = LoadAllyFixture();
        var map = FirmwareMap.Shipped();

        var badList = KnownBadList.Parse("""
        {
          "entries": [
            {
              "maker": "Asus",
              "model": "RC73XA",
              "kind": "bios",
              "version": "319",
              "reason": "Pulled for testing.",
              "source": "https://example.com"
            }
          ]
        }
        """);

        var biosGuid = "68e0fa36-7c6c-4861-ba1b-d10fbde48a1c";
        var badItem = CreateItem(
            $"fw:{biosGuid}",
            "BIOS Update for Windows",
            UpdateKind.Bios,
            "319",
            "318",
            "asus",
            InstallMethod.HandOff);

        var result = Merger.Merge([badItem], ally, badList, map);
        Assert.Single(result);
        Assert.Equal(ItemState.Blocked, result[0].State);
        Assert.Equal("Pulled for testing.", result[0].BlockedReason);
    }

    [Fact]
    public void Merge_UnparseableNeverBeatsParseable()
    {
        var ally = LoadAllyFixture();
        var knownBad = KnownBadList.Shipped();
        var map = FirmwareMap.Shipped();

        var unparseable = CreateItem(
            "dev:test",
            "Test Driver",
            UpdateKind.Driver,
            "Release_Candidate_Beta",
            "1.0.0",
            "source-a",
            InstallMethod.MakerPackage);

        var parseable = CreateItem(
            "dev:test",
            "Test Driver",
            UpdateKind.Driver,
            "2.0.0",
            "1.0.0",
            "source-b",
            InstallMethod.MakerPackage);

        var result = Merger.Merge([unparseable, parseable], ally, knownBad, map);
        Assert.Single(result);
        Assert.Equal("2.0.0", result[0].Item.LatestVersion);
        Assert.Equal("source-b", result[0].Item.SourceId);
    }
}
