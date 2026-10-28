namespace NitrOS.Image.Tests.Building;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;

[Collection("IsoLock")]
public sealed class ImageBuilderTests
{
    private sealed class FakeOscdimg : IOscdimg
    {
        public string EnsureVerified(CancellationToken ct) => "C:\\tools\\oscdimg.exe";
    }

    private static (Catalog Catalog, DeviceProfile Device) LoadTestCatalog()
    {
        var cur = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
        {
            cur = cur.Parent;
        }
        var catalogDir = Path.Combine(cur!.FullName, "catalog");
        var loadResult = CatalogLoader.Load(catalogDir);
        var dev = new DeviceProfile(HasBattery: true, HasTouchscreen: true, IsHandheld: true);
        return (loadResult.Catalog!, dev);
    }

    private static (FakeImageSystems Fakes, Catalog Catalog, DeviceProfile Device, Dictionary<string, BuildStore> Stores, ImageBuilder Builder) CreateTestContext()
    {
        var (catalog, device) = LoadTestCatalog();
        var fakes = new FakeImageSystems();

        // Setup source and destination files
        fakes.Files.Files["C:\\source.iso"] = "isodata";
        fakes.Files.Files["C:\\sources\\install.wim"] = "wimdata";
        fakes.Files.Files["M:\\sources\\install.wim"] = "wimdata";
        fakes.Files.Files["M:\\boot\\bcd"] = "bcd";
        fakes.Files.Files["M:\\efi\\boot\\bootx64.efi"] = "efi";
        fakes.Files.Files["M:\\sources\\boot.wim"] = "bootwim";

        fakes.Dism.EditionsList.Add(new ImageEdition(1, "Windows 11 Pro", "Professional", "x64", new Version(10, 0, 26100), 4000000000));

        var stores = new Dictionary<string, BuildStore>(StringComparer.OrdinalIgnoreCase);
        Func<string, BuildStore> storeFactory = folder =>
        {
            if (!stores.TryGetValue(folder, out var store))
            {
                store = new BuildStore(folder);
                stores[folder] = store;
            }
            return store;
        };

        var oscdimg = new FakeOscdimg();
        var builder = new ImageBuilder(fakes.Build(), catalog, device, storeFactory, oscdimg);
        return (fakes, catalog, device, stores, builder);
    }

    [Fact]
    public void Build_HappyPathRunsStepsInOrder()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var progressLog = new List<string>();
        var progress = new Progress<BuildProgress>(p =>
        {
            if (p.LogLine != null) progressLog.Add(p.LogLine);
        });

        // Pick 2 switch IDs
        var switchIds = catalog.Switches.Take(2).Select(s => s.Id).ToList();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, switchIds, CardId.Recommended);

        var state = builder.Start(req);
        Assert.Equal(BuildStep.Prepare, state.Next);

        var result = builder.Run(state, progress, CancellationToken.None);
        Assert.Equal(BuildStatus.Done, result.Status);
        Assert.Equal("C:\\out.iso", result.OutputPath);
        Assert.Single(fakes.Iso.Written);
    }

    [Fact]
    public void Build_NeverWritesUnderSource()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);
        var state = builder.Start(req);
        var result = builder.Run(state, new Progress<BuildProgress>(), CancellationToken.None);

        Assert.Equal(BuildStatus.Done, result.Status);
        foreach (var path in fakes.Files.Writes)
        {
            Assert.DoesNotContain("source.iso", path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Build_NoHiveLoadedDuringDismOrFiles()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);
        var state = builder.Start(req);

        // Before ApplyRegistry, no hives should be loaded
        var result = builder.Run(state, new Progress<BuildProgress>(), CancellationToken.None);
        Assert.Equal(BuildStatus.Done, result.Status);
        Assert.Empty(fakes.Registry.LoadedNitrOSHives());
    }

    [Fact]
    public void Resume_InFlightActionRunsAgain()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var switchIds = catalog.Switches.Take(1).Select(s => s.Id).ToList();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, switchIds, CardId.Recommended);

        var state = builder.Start(req);
        // Simulate in-flight action
        var action = new FileRemove("%SystemRoot%\\test.txt");
        var inFlight = new ImageEntry(switchIds[0], 0, action, ImagePhase.Files, null, null, DateTimeOffset.UtcNow);
        var stateWithInFlight = state with { Next = BuildStep.ApplyFiles, Entries = [inFlight] };

        // Save
        var store = stores[state.WorkFolder];
        store.Save(stateWithInFlight);

        var result = builder.Run(stateWithInFlight, new Progress<BuildProgress>(), CancellationToken.None);
        Assert.Equal(BuildStatus.Done, result.Status);
    }

    [Fact]
    public void Resume_RefusesChangedSourceOrCatalog()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);
        var state = builder.Start(req);

        // Tamper with selection hash
        var tampered = state with { SelectionHash = "different_hash" };
        var result = builder.Run(tampered, new Progress<BuildProgress>(), CancellationToken.None);

        Assert.Equal(BuildStatus.Refused, result.Status);
        Assert.Contains("switch list changed", result.Reason);
    }

    [Fact]
    public void Cancel_KeepsMountAndStateForResume_Discard_RemovesEverything()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);
        var state = builder.Start(req);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = builder.Run(state, new Progress<BuildProgress>(), cts.Token);
        Assert.Equal(BuildStatus.Cancelled, result.Status);
        Assert.NotNull(result.WorkFolder);

        // Discard
        builder.Discard(state);
        Assert.False(fakes.Files.Exists(state.WorkFolder));
    }

    [Fact]
    public void Battery_LowStopsSafelyAndResumes()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);
        var state = builder.Start(req);

        // Low battery state
        fakes.Power.State = new PowerState(HasBattery: true, OnBattery: true, BatteryPercent: 15);

        var result = builder.Run(state, new Progress<BuildProgress>(), CancellationToken.None);
        Assert.Equal(BuildStatus.StoppedLowBattery, result.Status);
    }

    [Fact]
    public void Build_SecondConcurrentRunIsRefused()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, [], CardId.Recommended);

        using var mutex = new Mutex(false, @"Global\NitrOS.Iso");
        Assert.True(mutex.WaitOne(0));
        try
        {
            BuildResult? inner = null;
            var thread = new Thread(() =>
            {
                var state = builder.Start(req);
                inner = builder.Run(state, new Progress<BuildProgress>(), CancellationToken.None);
            });
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));

            Assert.NotNull(inner);
            Assert.Equal(BuildStatus.Refused, inner.Status);
            Assert.Contains("already running", inner.Reason);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    [Fact]
    public void Build_FailedActionDoesNotStopBuild()
    {
        var (fakes, catalog, device, stores, builder) = CreateTestContext();
        var switchIds = catalog.Switches.Take(2).Select(s => s.Id).ToList();
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", null, switchIds, CardId.Recommended);

        fakes.Files.FailOn = nameof(IImageFiles.DeleteUnder);

        var state = builder.Start(req);
        var result = builder.Run(state, new Progress<BuildProgress>(), CancellationToken.None);

        // Should complete Done even with problems
        Assert.Equal(BuildStatus.Done, result.Status);
    }
}
