using System.Net;
using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Net;
using NitrOS.Updates.Sources;
using NitrOS.Updates.Tests.Fakes;
using Xunit;

namespace NitrOS.Updates.Tests.Sources;

public sealed class AsusSourceTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    private static (string biosJson, string driversJson) LoadAsusFixtures()
    {
        var biosPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "asus-RC73XA-bios-2026-10-28.json");
        var driversPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "asus-RC73XA-drivers-2026-10-28.json");
        return (File.ReadAllText(biosPath), File.ReadAllText(driversPath));
    }

    private static HttpClient CreateFakeClient(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
    {
        var handler = new FakeHttpHandler { OnSend = handlerFunc };
        return HttpFactory.Create(handler);
    }

    [Fact]
    public async Task Asus_BiosItemFromListing()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        var biosItem = result.Items.FirstOrDefault(i => i.Kind == UpdateKind.Bios && i.Name.Contains("BIOS Update for Windows"));
        Assert.NotNull(biosItem);
        Assert.Equal("319", biosItem.LatestVersion);
        Assert.Equal("319", biosItem.InstalledVersion);
        Assert.Equal(InstallMethod.HandOff, biosItem.Method);
        Assert.Equal("fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c", biosItem.Key);
        Assert.NotNull(biosItem.Sha256);
        Assert.StartsWith("https://dlcdnets.asus.com/", biosItem.DownloadUrl!.ToString());
    }

    [Fact]
    public async Task Asus_PdAndTouchFirmware()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        var pdItem = result.Items.FirstOrDefault(i => i.Name.Contains("PD_FW"));
        Assert.NotNull(pdItem);
        Assert.Equal("0020", pdItem.LatestVersion);
        Assert.Equal("0015", pdItem.InstalledVersion);
        Assert.Equal("fw:cbf5a488-f654-3fce-b3fc-8a1d94ead498", pdItem.Key);

        var touchItem = result.Items.FirstOrDefault(i => i.Name.Contains("Touch_Panel_FW"));
        Assert.NotNull(touchItem);
        Assert.Equal("2002", touchItem.LatestVersion);
        Assert.Equal("2002", touchItem.InstalledVersion);
        Assert.Equal("fw:222a79d6-d2aa-358b-b1ac-8771faf8fec0", touchItem.Key);
    }

    [Fact]
    public async Task Asus_McuToolIsCantTell()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        var mcuItem = result.Items.FirstOrDefault(i => i.Name.Contains("MCU"));
        Assert.NotNull(mcuItem);
        Assert.Null(mcuItem.InstalledVersion);
        Assert.Equal(InstallMethod.HandOff, mcuItem.Method);
    }

    [Fact]
    public async Task Asus_DriverMatchedByHardwareId()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        var amdItem = result.Items.FirstOrDefault(i => i.Name.Contains("AMD Graphics Driver"));
        Assert.NotNull(amdItem);
        Assert.StartsWith("dev:PCI\\VEN_1002&DEV_150E", amdItem.Key);
        Assert.Equal("32.0.32015.7029", amdItem.InstalledVersion);
    }

    [Fact]
    public async Task Asus_KeepsSystemControlInterface()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        var sciItem = result.Items.FirstOrDefault(i => i.Name.Contains("ASUS System Control Interface v3"));
        Assert.NotNull(sciItem);
        Assert.StartsWith("dev:ACPI\\ASUS2018", sciItem.Key);
    }

    [Fact]
    public async Task Asus_DropsArmouryCrateAndStoreLinks()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        Assert.DoesNotContain(result.Items, i => i.Name.StartsWith("Armoury Crate SE Installer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.Name.StartsWith("MyASUS", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.Name.StartsWith("GlideX", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.Name.StartsWith("Dolby", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.Name.Contains("EZ Flash", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, i => i.Name.Contains("OS Assistant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Asus_DriverInstallsHandOffUntilProven()
    {
        var ally = LoadAllyFixture();
        var (biosJson, driversJson) = LoadAsusFixtures();
        var client = CreateFakeClient(req =>
        {
            var content = req.RequestUri!.ToString().Contains("GetPDBIOS") ? biosJson : driversJson;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) };
        });

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Ok, result.Status);
        foreach (var item in result.Items)
        {
            Assert.Equal(InstallMethod.HandOff, item.Method);
        }
    }

    [Fact]
    public async Task Asus_HttpErrorFallsBackToSupportPage()
    {
        var ally = LoadAllyFixture();
        var client = CreateFakeClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var source = new AsusSource(client, FirmwareMap.Shipped());
        var result = await source.CheckAsync(ally, CancellationToken.None);

        Assert.Equal(SourceStatus.Unavailable, result.Status);
        Assert.NotNull(result.FallbackPage);
        Assert.Contains("rog.asus.com", result.FallbackPage.ToString());
    }

    [Fact]
    public async Task Asus_NotForOtherMakers()
    {
        var ally = LoadAllyFixture() with { Maker = MakerId.Lenovo };
        var client = CreateFakeClient(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var source = new AsusSource(client, FirmwareMap.Shipped());
        Assert.False(source.AppliesTo(ally));
        var result = await source.CheckAsync(ally, CancellationToken.None);
        Assert.Equal(SourceStatus.NotForThisDevice, result.Status);
    }
}
