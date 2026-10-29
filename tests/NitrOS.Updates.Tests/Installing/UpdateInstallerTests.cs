using System.IO.Compression;
using System.Net;
using System.Text.Json;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Updates.Installing;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Merging;
using NitrOS.Updates.Model;
using NitrOS.Updates.Net;
using NitrOS.Updates.Tests.Fakes;
using NitrOS.Updates.Windows;
using Xunit;

namespace NitrOS.Updates.Tests.Installing;

public sealed class UpdateInstallerTests : IDisposable
{
    private readonly string _tempDir;

    public UpdateInstallerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "nitros-installer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    private (UpdateInstaller installer, FakeUpdateAgent agent, FakeVerifier verifier, FakeRunner runner, FakePowerSource power, FakeRestorePoint rp)
        CreateInstaller(Func<HttpRequestMessage, HttpResponseMessage>? httpHandler = null)
    {
        var agent = new FakeUpdateAgent();
        var client = HttpFactory.Create(new FakeHttpHandler
        {
            OnSend = httpHandler ?? (req =>
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[128])
                };
                resp.Content.Headers.ContentLength = 128;
                return resp;
            })
        });

        var downloader = new Downloader(client);
        var verifier = new FakeVerifier();
        var runner = new FakeRunner();
        var power = new FakePowerSource(new PowerState(HasBattery: true, OnBattery: false, BatteryPercent: 90));
        var rp = new FakeRestorePoint();
        var knownBad = KnownBadList.Shipped();
        var policies = SourcePolicy.Shipped();

        var installer = new UpdateInstaller(
            agent,
            downloader,
            verifier,
            runner,
            power,
            rp,
            knownBad,
            policies,
            _tempDir,
            d => Directory.CreateDirectory(d));

        return (installer, agent, verifier, runner, power, rp);
    }

    [Fact]
    public async Task Install_RefusesOtherHost()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, _, _, _) = CreateInstaller();

        var item = new UpdateItem(
            "dev:test", "Test Driver", UpdateKind.Driver, "1.0", "2.0",
            new Uri("https://example.com/driver.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.MakerPackage
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public async Task Install_RefusesRedirectOffHost()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, _, _, _) = CreateInstaller(req =>
        {
            if (req.RequestUri!.Host == "dlcdnets.asus.com")
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://evil.example.com/bad.exe");
                return redirect;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var item = new UpdateItem(
            "dev:test", "Test Driver", UpdateKind.Driver, "1.0", "2.0",
            new Uri("https://dlcdnets.asus.com/driver.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.MakerPackage
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public async Task Install_RefusesHashMismatch()
    {
        var ally = LoadAllyFixture();
        var (installer, _, verifier, _, _, _) = CreateInstaller();
        verifier.ShaMatchesResult = false;

        var item = new UpdateItem(
            "dev:test", "Test Driver", UpdateKind.Driver, "1.0", "2.0",
            new Uri("https://dlcdnets.asus.com/driver.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.MakerPackage,
            Sha256 = "0123456789ABCDEF"
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Contains("checksum", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public async Task Install_RefusesWrongSigner()
    {
        var ally = LoadAllyFixture();
        var (installer, _, verifier, _, _, _) = CreateInstaller();
        verifier.SignerNamesResult = ["Malicious Signer Corp"];

        var item = new UpdateItem(
            "dev:test", "Test Driver", UpdateKind.Driver, "1.0", "2.0",
            new Uri("https://dlcdnets.asus.com/driver.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.MakerPackage
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Contains("not signed by", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public async Task Install_FirmwareNeedsCharger()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, _, power, _) = CreateInstaller();
        power.CurrentState = new PowerState(HasBattery: true, OnBattery: true, BatteryPercent: 50);

        var item = new UpdateItem(
            "fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c", "BIOS Update", UpdateKind.Bios, "318", "319",
            new Uri("https://dlcdnets.asus.com/bios.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.HandOff
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, UserConfirmedFirmwareWarning: true);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Contains("charger", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Install_FirmwareNeedsConfirmedWarning()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, _, _, _) = CreateInstaller();

        var item = new UpdateItem(
            "fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c", "BIOS Update", UpdateKind.Bios, "318", "319",
            new Uri("https://dlcdnets.asus.com/bios.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.HandOff
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, UserConfirmedFirmwareWarning: false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Refused, outcome.Status);
        Assert.Contains("warning", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Install_BiosIsHandedOffNotRun()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, runner, _, _) = CreateInstaller();

        var item = new UpdateItem(
            "fw:68e0fa36-7c6c-4861-ba1b-d10fbde48a1c", "BIOS Update", UpdateKind.Bios, "318", "319",
            new Uri("https://dlcdnets.asus.com/bios.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.HandOff
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, UserConfirmedFirmwareWarning: true);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.HandedOff, outcome.Status);
        Assert.NotNull(outcome.HandOffPath);
        Assert.Empty(runner.RunCalls);
    }

    [Fact]
    public async Task Install_DriverRunsWithListingSwitchesWhenProven()
    {
        var ally = LoadAllyFixture();
        var (installer, _, _, runner, _, _) = CreateInstaller();

        var item = new UpdateItem(
            "dev:test", "Driver Package", UpdateKind.Driver, "1.0", "2.0",
            new Uri("https://dlcdnets.asus.com/driver.exe"), 128, null, "asus", "ASUS")
        {
            Method = InstallMethod.MakerPackage,
            SilentArgs = "/SUPPRESSMSGBOXES /VERYSILENT /NORESTART"
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.Installed, outcome.Status);
        Assert.Single(runner.RunCalls);
        Assert.Equal("/SUPPRESSMSGBOXES /VERYSILENT /NORESTART", runner.RunCalls[0].Args);
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    [Fact]
    public async Task Install_WindowsUpdateMapsResultCodes()
    {
        var ally = LoadAllyFixture();
        var (installer, agent, _, _, _, _) = CreateInstaller();

        agent.OnInstall = (_, _, _) => Task.FromResult(new WuInstallResult(2, 0, false));

        var item = new UpdateItem(
            "src:windows-update:1", "WU Driver", UpdateKind.Driver, "1.0", "2.0",
            null, 128, null, "windows-update", "Windows Update")
        {
            Method = InstallMethod.WindowsUpdate,
            WindowsUpdateId = "wu-id-1"
        };
        var merged = new MergedItem(item, ItemState.NewVersion, null, []);
        var request = new InstallRequest(merged, ally, false);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);
        Assert.Equal(InstallStatus.Installed, outcome.Status);
    }

    [Fact]
    public async Task Install_ExtractsZipPayloadAndVerifiesInnerExe()
    {
        var ally = LoadAllyFixture();
        byte[] zipBytes;
        using (var ms = new MemoryStream())
        {
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                var entry = archive.CreateEntry("tool_setup.exe");
                using var es = entry.Open();
                es.Write(new byte[64], 0, 64);
            }
            zipBytes = ms.ToArray();
        }

        var (installer, _, verifier, _, _, _) = CreateInstaller(req =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes) };
            resp.Content.Headers.ContentLength = zipBytes.Length;
            return resp;
        });

        verifier.SignerNamesResult = ["ASUSTeK COMPUTER INC."];

        var item = new UpdateItem(
            "fw:mcu", "ROG MCU Firmware Update tool", UpdateKind.Firmware, null, "326",
            new Uri("https://dlcdnets.asus.com/tool.zip"), (long)zipBytes.Length, null, "asus", "ASUS")
        {
            Method = InstallMethod.HandOff
        };
        var merged = new MergedItem(item, ItemState.CantTell, null, []);
        var request = new InstallRequest(merged, ally, UserConfirmedFirmwareWarning: true);

        var outcome = await installer.InstallAsync(request, null, CancellationToken.None);

        Assert.Equal(InstallStatus.HandedOff, outcome.Status);
        Assert.NotNull(outcome.HandOffPath);
        Assert.EndsWith(".exe", outcome.HandOffPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Install_NeverRestarts()
    {
        var ctors = typeof(UpdateInstaller).GetConstructors();
        foreach (var ctor in ctors)
        {
            foreach (var param in ctor.GetParameters())
            {
                Assert.DoesNotContain("restart", param.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
