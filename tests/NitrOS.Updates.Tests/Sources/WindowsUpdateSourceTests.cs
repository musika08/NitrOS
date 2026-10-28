using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Sources;
using NitrOS.Updates.Tests.Fakes;
using NitrOS.Updates.Windows;
using Xunit;

namespace NitrOS.Updates.Tests.Sources;

public sealed class WindowsUpdateSourceTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    private static IReadOnlyList<WuRecord> LoadWuFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "wu-ally-x-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<WuRecord>>(json, options)!;
    }

    [Fact]
    public async Task Wu_FirmwareOnSystemResourceIsBios()
    {
        var ally = LoadAllyFixture();
        var records = LoadWuFixture();
        var map = FirmwareMap.Shipped();
        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<WuRecord>>([records[0]])
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(UpdateKind.Bios, item.Kind);
        Assert.Equal("fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c", item.Key);
        Assert.Equal("319", item.InstalledVersion);
        Assert.Equal("10.1.2.319", item.LatestVersion);
    }

    [Fact]
    public async Task Wu_FirmwareOnDeviceResourceIsFirmware()
    {
        var ally = LoadAllyFixture();
        var records = LoadWuFixture();
        var map = FirmwareMap.Shipped();
        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<WuRecord>>([records[1]])
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(UpdateKind.Firmware, item.Kind);
        Assert.Equal("fw:cbf5a488-f654-3fce-b3fc-8a1d94ead498", item.Key);
        Assert.Equal("0015", item.InstalledVersion);
        Assert.Equal("10.1.2.20", item.LatestVersion);
    }

    [Fact]
    public async Task Wu_DriverMatchedToPresentDevice()
    {
        var ally = LoadAllyFixture();
        var records = LoadWuFixture();
        var map = FirmwareMap.Shipped();
        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<WuRecord>>([records[2]])
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(UpdateKind.Driver, item.Kind);
        Assert.StartsWith("dev:", item.Key);
        Assert.Equal("11.19.602.2025", item.InstalledVersion);
        Assert.Equal("11.19.20.602", item.LatestVersion);
    }

    [Fact]
    public async Task Wu_UnmatchedDriverHasOwnKey()
    {
        var ally = LoadAllyFixture();
        var map = FirmwareMap.Shipped();
        var unmatched = new WuRecord(
            "unknown-guid",
            1,
            "Unknown Hardware Driver - 1.0.0",
            "System",
            "PCI\\VEN_9999&DEV_9999",
            "Unknown",
            "Unknown",
            "Unknown",
            null,
            null,
            false,
            true,
            false,
            0);

        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<WuRecord>>([unmatched])
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Single(result.Items);
        Assert.Equal("src:windows-update:unknown-guid", result.Items[0].Key);
    }

    [Fact]
    public async Task Wu_SearchFailureIsUnavailable()
    {
        var ally = LoadAllyFixture();
        var map = FirmwareMap.Shipped();
        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => throw new InvalidOperationException("Network down")
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.Equal(UpdateTexts.WindowsUpdateUnavailable, result.Problem);
        Assert.Null(result.FallbackPage);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Wu_EmptyIsOkWithNoItems()
    {
        var ally = LoadAllyFixture();
        var map = FirmwareMap.Shipped();
        var fakeAgent = new FakeUpdateAgent
        {
            OnSearch = _ => Task.FromResult<IReadOnlyList<WuRecord>>(Array.Empty<WuRecord>())
        };

        var source = new WindowsUpdateSource(fakeAgent, map);
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.Empty(result.Items);
    }
}
