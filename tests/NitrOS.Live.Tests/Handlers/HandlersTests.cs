using NitrOS.Core.Model;
using NitrOS.Live.Handlers;

namespace NitrOS.Live.Tests.Handlers;

public class HandlersTests
{
    [Fact]
    public void Handlers_ForReturnsAHandlerForEachSupportedType()
    {
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new RegistrySet("HKLM", "K", "N", RegistryKind.Dword, "1")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new RegistryDelete("HKLM", "K")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new StartupDisable(StartupSource.Task, "\\T")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new LaunchAtLogon("a.exe")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new ServiceStart("S", ServiceStartType.Disabled)));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new TaskDisable("\\T")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new PowerSet("G", "S", 1)));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new PowerScheme("G")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new AppxRemove("X")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new CapabilityRemove("X")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new FeatureDisable("X")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new FileRemove("X")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new ProgramUninstall("X", "P")));
        Assert.IsAssignableFrom<IActionHandler>(NitrOS.Live.Handlers.Handlers.For(new Script("x")));
    }

    [Fact]
    public void Handlers_ForUnknownTypeThrows()
    {
        Assert.Throws<NotSupportedException>(() => NitrOS.Live.Handlers.Handlers.For(new UnknownAction()));
    }

    [Fact]
    public void Handlers_PackageRemoveIsImageOnly()
    {
        // package.remove services an offline image; the PC tuner has no handler for it.
        Assert.Throws<NotSupportedException>(() => NitrOS.Live.Handlers.Handlers.For(new PackageRemove("Microsoft-Windows-Foo*")));
    }

    private sealed record UnknownAction : SwitchAction;
}
