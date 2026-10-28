using System.Text.Json;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;
using NitrOS.Updates.Checking;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;
using NitrOS.Updates.Sources;
using NitrOS.Updates.Tests.Fakes;
using Xunit;

namespace NitrOS.Updates.Tests.Checking;

public sealed class UpdateCheckerTests
{
    private static DeviceInfo LoadAllyFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ally-x-device-2026-10-28.json");
        var json = File.ReadAllText(fixturePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return JsonSerializer.Deserialize<DeviceInfo>(json, options)!;
    }

    private static (FakeDeviceReader reader, FakeRegistry reg, WindowsUpdatePolicy policy) CreateEnvironment(DeviceInfo device, int excludeFlag = 0)
    {
        var reader = new FakeDeviceReader(device);
        var reg = new FakeRegistry();
        if (excludeFlag != 0)
        {
            reg.SetValue(
                LiveHive.LocalMachine,
                @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                "ExcludeWUDriversInQualityUpdate",
                new RegValue(RegistryKind.Dword, excludeFlag.ToString()),
                RegView.Default);
        }
        var policy = new WindowsUpdatePolicy(reg);
        return (reader, reg, policy);
    }

    [Fact]
    public async Task Checker_RunsOnlyApplicableSources()
    {
        var ally = LoadAllyFixture();
        var (reader, _, policy) = CreateEnvironment(ally);

        var calledApplicable = false;
        var calledNonApplicable = false;

        var s1 = new FakeSource("s1", "S1")
        {
            OnAppliesTo = _ => true,
            OnCheck = (_, _) =>
            {
                calledApplicable = true;
                return Task.FromResult(new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, null));
            }
        };

        var s2 = new FakeSource("s2", "S2")
        {
            OnAppliesTo = _ => false,
            OnCheck = (_, _) =>
            {
                calledNonApplicable = true;
                return Task.FromResult(new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, null));
            }
        };

        var checker = new UpdateChecker(reader, [s1, s2], KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.True(calledApplicable);
        Assert.False(calledNonApplicable);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task Checker_SourceFailureBecomesNoticeWithPage()
    {
        var ally = LoadAllyFixture();
        var (reader, _, policy) = CreateEnvironment(ally);

        var fallbackUri = new Uri("https://rog.asus.com/support/");
        var asusFail = new FakeSource("asus", "ASUS")
        {
            OnCheck = (_, _) => Task.FromResult(new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), "Network timeout", fallbackUri))
        };

        var wuItem = new UpdateItem("dev:test", "Test Device", UpdateKind.Driver, "1.0", "2.0", null, null, null, "wu", "WU");
        var wuOk = new FakeSource("wu", "Windows Update")
        {
            OnCheck = (_, _) => Task.FromResult(new SourceResult(SourceStatus.Ok, [wuItem], null, null))
        };

        var checker = new UpdateChecker(reader, [asusFail, wuOk], KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.Single(report.Notices);
        Assert.Equal("ASUS", report.Notices[0].SourceName);
        Assert.Equal("Network timeout", report.Notices[0].Problem);
        Assert.Equal(fallbackUri, report.Notices[0].Page);

        Assert.Single(report.Items);
        Assert.Equal("Test Device", report.Items[0].Item.Name);
    }

    [Fact]
    public async Task Checker_ThrowingSourceDoesNotStopOthers()
    {
        var ally = LoadAllyFixture();
        var (reader, _, policy) = CreateEnvironment(ally);

        var throwingSource = new FakeSource("bad", "BadSource")
        {
            OnCheck = (_, _) => throw new InvalidOperationException("Fatal boom")
        };

        var goodItem = new UpdateItem("dev:good", "Good Device", UpdateKind.Driver, "1.0", "2.0", null, null, null, "good", "Good");
        var goodSource = new FakeSource("good", "GoodSource")
        {
            OnCheck = (_, _) => Task.FromResult(new SourceResult(SourceStatus.Ok, [goodItem], null, null))
        };

        var checker = new UpdateChecker(reader, [throwingSource, goodSource], KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.Single(report.Notices);
        Assert.Contains("Fatal boom", report.Notices[0].Problem);
        Assert.Single(report.Items);
    }

    [Fact]
    public async Task Checker_SupportPageSourceBecomesLink()
    {
        var msiDevice = LoadAllyFixture() with { Maker = MakerId.Msi };
        var (reader, _, policy) = CreateEnvironment(msiDevice);

        var msiPageUri = new Uri("https://www.msi.com/support/download");
        var msiSource = new FakeSource("page-msi", "MSI")
        {
            OnCheck = (_, _) => Task.FromResult(new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, msiPageUri))
        };

        var checker = new UpdateChecker(reader, [msiSource], KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.Single(report.Links);
        Assert.Equal("Open MSI's support page", report.Links[0].Text);
        Assert.Equal(msiPageUri, report.Links[0].Page);
    }

    [Fact]
    public async Task Checker_OtherMakerSaysNoList()
    {
        var otherDevice = LoadAllyFixture() with { Maker = MakerId.Other, Manufacturer = "Acme Corp" };
        var (reader, _, policy) = CreateEnvironment(otherDevice);

        var checker = new UpdateChecker(reader, Array.Empty<IUpdateSource>(), KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.Single(report.Notices);
        Assert.Contains("Acme Corp", report.Notices[0].Problem);
        Assert.Null(report.Notices[0].Page);
    }

    [Fact]
    public async Task Checker_ReportsPolicyFlag()
    {
        var ally = LoadAllyFixture();
        var (reader, reg, policy) = CreateEnvironment(ally, excludeFlag: 1);

        var checker = new UpdateChecker(reader, Array.Empty<IUpdateSource>(), KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        var writesBefore = reg.Writes.Count;
        var report = await checker.CheckAsync(null, CancellationToken.None);

        Assert.True(report.WindowsUpdateDriversExcluded);
        Assert.Equal(writesBefore, reg.Writes.Count);
    }

    [Fact]
    public async Task Checker_CancelStopsAndThrowsOperationCanceled()
    {
        var ally = LoadAllyFixture();
        var (reader, _, policy) = CreateEnvironment(ally);

        var slowSource = new FakeSource("slow", "Slow")
        {
            OnCheck = async (_, ct) =>
            {
                await Task.Delay(5000, ct);
                return new SourceResult(SourceStatus.Ok, Array.Empty<UpdateItem>(), null, null);
            }
        };

        var checker = new UpdateChecker(reader, [slowSource], KnownBadList.Shipped(), FirmwareMap.Shipped(), policy, TimeProvider.System);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(null, cts.Token));
    }
}
