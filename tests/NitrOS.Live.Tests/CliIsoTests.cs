namespace NitrOS.Live.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NitrOS.Cli;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Image.Building;
using NitrOS.Image.Systems;
using NitrOS.Image.Tests.Fakes;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

public sealed class CliIsoTests
{
    private sealed record Device(bool HasBattery, int MaxTouches, string Manufacturer, string Model) : IDeviceFacts;
    private sealed record Session(string ProcessUserSid, string? ConsoleUserSid, bool IsElevated, int WindowsBuild) : ISessionFacts;

    private sealed class Probe : IHealthProbe
    {
        public bool IsProcessRunning(string processName) => true;
        public bool IsServiceRunning(string serviceName) => true;
        public bool IsPackageInstalled(string packageName) => true;
        public string? HttpGet(string url) => HealthCheck.ConnectTestBody;
    }

    private static (FakeImageSystems Image, FakeOscdimg Oscdimg, CliEnvironment Env, Catalog Catalog) CreateEnv()
    {
        var fakes = new FakeImageSystems();
        var oscdimg = new FakeOscdimg();

        var root = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(root);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "catalog")))
        {
            dir = dir.Parent;
        }
        var catDir = Path.Combine(dir!.FullName, "catalog");
        var catalog = CatalogLoader.Load(catDir).Catalog!;

        fakes.Dism.EditionsList.Add(new ImageEdition(1, "Windows 11 Pro", "Professional", "x64", new Version(10, 0, 26100), 4000000000));
        fakes.Dism.EditionsList.Add(new ImageEdition(2, "Windows 11 Home", "Core", "x64", new Version(10, 0, 26100), 4000000000));

        fakes.Files.Files["C:\\source.iso"] = "iso";
        fakes.Files.Files["M:\\sources\\install.wim"] = "wim";
        fakes.Files.Files["M:\\boot\\bcd"] = "bcd";
        fakes.Files.Files["M:\\efi\\boot\\bootx64.efi"] = "efi";
        fakes.Files.Files["M:\\sources\\boot.wim"] = "boot";

        var liveFakes = new FakeSystems();
        var env = new CliEnvironment(
            () => liveFakes.ToLive(),
            new Device(false, 0, "Test", "Desk"),
            new Session("S-1-5-21-1", "S-1-5-21-1", true, 26200),
            new Probe(),
            catDir,
            "C:\\journals")
        {
            Image = () => fakes.Build(),
            Oscdimg = _ => oscdimg
        };

        return (fakes, oscdimg, env, catalog);
    }

    [Fact]
    public void BuildIso_RefusesWithoutYes()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Refused, code);
        Assert.Contains("Refused: build-iso creates a new Windows installation media", sw.ToString());
    }

    [Fact]
    public void BuildIso_RefusesWhenSessionGuardFails()
    {
        var (_, _, env, _) = CreateEnv();
        var nonElevatedEnv = env with { Session = new Session("S-1-5-21-1", "S-1-5-21-1", false, 26200) };
        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended", "--yes"], sw, nonElevatedEnv, CancellationToken.None);

        Assert.Equal(CliApp.Refused, code);
        Assert.Contains("administrator rights", sw.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildIso_PrintsResultsAndPath()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended", "--yes"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Ok, code);
        var output = sw.ToString();
        Assert.Contains("Output: C:\\out.iso", output);
    }

    [Fact]
    public void BuildIso_ResumesExistingBuild()
    {
        var (fakes, _, env, catalog) = CreateEnv();
        // Seed a resumable build
        var workFolder = "C:\\NitrOS-work-resume123";
        fakes.Files.Files["C:\\NitrOS-work-resume123\\state.json"] = "dummy";
        var store = new BuildStore(workFolder);
        var dev = new DeviceProfile(true, true, true);
        var sel = Selection.FromCard(catalog, CardId.Recommended, TargetMode.BuildIso, dev);
        var activeSwitches = catalog.Switches.Where(s => sel.IsOn(s.Id)).ToList();
        var hash = SelectionHash.Of(activeSwitches);

        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", 1, sel.On.ToList(), CardId.Recommended);
        var state = new BuildState("resume123", req, hash, new SourceFingerprint(1024, DateTimeOffset.UtcNow), 1, "Professional", BuildStep.ExportImage, [], workFolder, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new NitrOS.Image.FirstBoot.FirstBootPlan());
        store.Save(state);

        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended", "--yes"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Ok, code);
        Assert.Contains("Resuming existing ISO build", sw.ToString());
    }

    [Fact]
    public void BuildIso_DiscardRemovesWork()
    {
        var (fakes, _, env, catalog) = CreateEnv();
        var workFolder = "C:\\NitrOS-work-discard123";
        fakes.Files.Files["C:\\NitrOS-work-discard123\\state.json"] = "dummy";
        var store = new BuildStore(workFolder);
        var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", 1, [], CardId.Recommended);
        var state = new BuildState("discard123", req, "hash", new SourceFingerprint(1024, DateTimeOffset.UtcNow), 1, "Professional", BuildStep.ExportImage, [], workFolder, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, new NitrOS.Image.FirstBoot.FirstBootPlan());
        store.Save(state);

        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--out", "C:\\out.iso", "--discard", "--yes"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Ok, code);
        Assert.Contains("Discarded unfinished build", sw.ToString());
    }

    [Fact]
    public void BuildIso_DiagnosticsRefusedOutsideVm()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended", "--stop-after", "mount", "--yes"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.BadArguments, code);
        Assert.Contains("Diagnostic options are only permitted when NITROS_VM=1", sw.ToString());
    }

    [Fact]
    public void ImageInfo_ListsEditions()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        var code = CliApp.Run(["image-info", "--source", "C:\\source.iso"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Ok, code);
        var output = sw.ToString();
        Assert.Contains("Index 1: Windows 11 Pro", output);
        Assert.Contains("Index 2: Windows 11 Home", output);
    }

    [Fact]
    public void Health_IsoCardUsesCardSet()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        var code = CliApp.Run(["health", "--iso-card", "max-lean"], sw, env, CancellationToken.None);

        Assert.Equal(CliApp.Ok, code);
        var output = sw.ToString();
        Assert.Contains("Health check for ISO preset", output);
    }

    [Fact]
    public void BadArguments_Exit2()
    {
        var (_, _, env, _) = CreateEnv();
        var sw = new StringWriter();
        // Both --card and --ids
        var code = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--card", "recommended", "--ids", "a,b", "--yes"], sw, env, CancellationToken.None);
        Assert.Equal(CliApp.BadArguments, code);

        // Neither --card nor --ids
        sw = new StringWriter();
        var code2 = CliApp.Run(["build-iso", "--source", "C:\\source.iso", "--out", "C:\\out.iso", "--yes"], sw, env, CancellationToken.None);
        Assert.Equal(CliApp.BadArguments, code2);
    }
}
