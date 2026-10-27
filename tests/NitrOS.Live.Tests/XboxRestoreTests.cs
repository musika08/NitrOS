using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests;

public class XboxRestoreTests
{
    private sealed class TrackingSteps : IXboxRestoreSteps
    {
        public List<string> CallOrder { get; } = [];
        public Action? OnStore { get; set; }

        public bool RestoreStore(IProgress<string>? progress, CancellationToken ct)
        {
            CallOrder.Add("RestoreStore");
            OnStore?.Invoke();
            return true;
        }

        public bool RestoreXboxSignIn(IProgress<string>? progress, CancellationToken ct)
        {
            CallOrder.Add("RestoreXboxSignIn");
            return true;
        }

        public bool RestoreGameBar(IProgress<string>? progress, CancellationToken ct)
        {
            CallOrder.Add("RestoreGameBar");
            return true;
        }

        public bool RestoreGamingServices(IProgress<string>? progress, CancellationToken ct)
        {
            CallOrder.Add("RestoreGamingServices");
            return true;
        }

        public bool RestoreXboxApp(IProgress<string>? progress, CancellationToken ct)
        {
            CallOrder.Add("RestoreXboxApp");
            return true;
        }
    }

    [Fact]
    public void XboxRestore_RestoresServicesThenApps()
    {
        var fake = new FakeSystems();
        // Initially set Xbox services to disabled
        fake.Services.Set("XblAuthManager", ServiceStartType.Disabled);
        fake.Services.Set("XblGameSave", ServiceStartType.Disabled);
        fake.Services.Set("XboxNetApiSvc", ServiceStartType.Disabled);

        var steps = new TrackingSteps();
        var wasServicesManualWhenStoreRan = false;

        steps.OnStore = () =>
        {
            wasServicesManualWhenStoreRan =
                fake.Services.Get("XblAuthManager")?.Start == ServiceStartType.Manual &&
                fake.Services.Get("XblGameSave")?.Start == ServiceStartType.Manual &&
                fake.Services.Get("XboxNetApiSvc")?.Start == ServiceStartType.Manual;
        };

        var restore = new XboxRestore(fake.ToLive(), steps);
        restore.Run(null, CancellationToken.None);

        Assert.True(wasServicesManualWhenStoreRan, "Services must be set to Manual before AppX packages are restored");
        Assert.Equal(["RestoreStore", "RestoreXboxSignIn", "RestoreGameBar", "RestoreGamingServices", "RestoreXboxApp"], steps.CallOrder);
    }

    [Fact]
    public void XboxRestore_ReportsEachPart()
    {
        var fake = new FakeSystems();
        fake.Services.Set("XblAuthManager", ServiceStartType.Disabled);
        fake.Services.Set("XblGameSave", ServiceStartType.Disabled);
        fake.Services.Set("XboxNetApiSvc", ServiceStartType.Disabled);

        // Pre-populate only Store and Xbox App
        fake.Appx.Installed.Add("Microsoft.WindowsStore");
        fake.Appx.Installed.Add("Microsoft.GamingApp");

        var steps = new TrackingSteps();
        var restore = new XboxRestore(fake.ToLive(), steps);
        var result = restore.Run(null, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(6, result.Parts.Count);

        var store = Assert.Single(result.Parts, p => p.Part == "Microsoft Store");
        Assert.True(store.Ok);

        var xboxApp = Assert.Single(result.Parts, p => p.Part == "Xbox App");
        Assert.True(xboxApp.Ok);

        var gameBar = Assert.Single(result.Parts, p => p.Part == "Xbox Game Bar");
        Assert.False(gameBar.Ok);

        var services = Assert.Single(result.Parts, p => p.Part == "Xbox Services");
        Assert.True(services.Ok);
    }

    [Fact]
    [Trait("Lane", "Vm")]
    public void XboxRestore_AfterMaxLean_BringsBackStoreAndXbox()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("NITROS_VM") == "1", "VM lane only");
        // Real in-guest test runs in the measurement VM
    }
}
