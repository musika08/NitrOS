namespace NitrOS.Image.Tests.Building;

using System;
using System.IO;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Image.Building;

public sealed class ImagePhasesTests
{
    [Fact]
    public void Phases_MapEveryImageType()
    {
        Assert.Equal(ImagePhase.Dism, ImagePhases.For(new AppxRemove("*pkg*")));
        Assert.Equal(ImagePhase.Dism, ImagePhases.For(new CapabilityRemove("cap")));
        Assert.Equal(ImagePhase.Dism, ImagePhases.For(new FeatureDisable("feat")));
        Assert.Equal(ImagePhase.Dism, ImagePhases.For(new PackageRemove("*pkg*")));

        Assert.Equal(ImagePhase.Files, ImagePhases.For(new FileRemove("file")));

        Assert.Equal(ImagePhase.Registry, ImagePhases.For(new RegistrySet("HKLM", "key", "val", RegistryKind.Dword, "1")));
        Assert.Equal(ImagePhase.Registry, ImagePhases.For(new RegistryDelete("HKLM", "key", "val")));
        Assert.Equal(ImagePhase.Registry, ImagePhases.For(new ServiceStart("svc", ServiceStartType.Disabled)));
        Assert.Equal(ImagePhase.Registry, ImagePhases.For(new StartupDisable(StartupSource.Run, "run")));

        Assert.Equal(ImagePhase.FirstBoot, ImagePhases.For(new TaskDisable("\\task")));
        Assert.Equal(ImagePhase.FirstBoot, ImagePhases.For(new PowerSet("sub", "set", 1, 0)));
        Assert.Equal(ImagePhase.FirstBoot, ImagePhases.For(new Script("echo")));
    }

    [Fact]
    public void Phases_LiveOnlyThrows()
    {
        Assert.Throws<InvalidOperationException>(() => ImagePhases.For(new ProgramUninstall("name", "pub", null)));
        Assert.Throws<InvalidOperationException>(() => ImagePhases.For(new PowerScheme("guid")));
        Assert.Throws<InvalidOperationException>(() => ImagePhases.For(new LaunchAtLogon("exe", null)));
    }

    [Fact]
    public void Phases_RealCatalogImageActionsAllMap()
    {
        var cur = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
        {
            cur = cur.Parent;
        }
        Assert.NotNull(cur);
        var catalogDir = Path.Combine(cur.FullName, "catalog");
        var loadResult = CatalogLoader.Load(catalogDir);
        Assert.NotNull(loadResult.Catalog);

        foreach (var sw in loadResult.Catalog.Switches)
        {
            foreach (var action in sw.Image)
            {
                var phase = ImagePhases.For(action);
                Assert.True(Enum.IsDefined(typeof(ImagePhase), phase));
            }
        }
    }
}
