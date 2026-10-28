namespace NitrOS.Image.Tests.Handlers;

using System;
using NitrOS.Core.Model;
using NitrOS.Image.Building;
using NitrOS.Image.FirstBoot;
using NitrOS.Image.Handlers;
using NitrOS.Image.Tests.Fakes;

public sealed class DeferredHandlersTests
{
    [Fact]
    public void Task_AlwaysDeferred()
    {
        var fakes = new FakeImageSystems();
        var plan = new FirstBootPlan();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, plan, "sw1");

        var action = new TaskDisable("\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator");
        var res = ImageHandlers.For(action).Apply(action, ctx);

        Assert.Equal(ImageOutcome.Deferred, res.Outcome);
        Assert.Single(plan.Machine);
        Assert.IsType<TaskOff>(plan.Machine[0]);
    }

    [Fact]
    public void Power_AlwaysDeferred()
    {
        var fakes = new FakeImageSystems();
        var plan = new FirstBootPlan();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, plan, "sw1");

        var action = new PowerSet("SUB_PROCESSOR", "PROCTHROTTLEMAX", 99, 99);
        var res = ImageHandlers.For(action).Apply(action, ctx);

        Assert.Equal(ImageOutcome.Deferred, res.Outcome);
        Assert.Single(plan.Machine);
        Assert.IsType<PowerValue>(plan.Machine[0]);
    }

    [Fact]
    public void Script_AlwaysDeferred()
    {
        var fakes = new FakeImageSystems();
        var plan = new FirstBootPlan();
        var ctx = new ImageContext(fakes.Build(), "M:\\mount", 1, plan, "sw1");

        var action = new Script("echo hi");
        var res = ImageHandlers.For(action).Apply(action, ctx);

        Assert.Equal(ImageOutcome.Deferred, res.Outcome);
        Assert.Single(plan.Machine);
        Assert.IsType<ScriptRun>(plan.Machine[0]);
    }

    [Fact]
    public void Handlers_LiveOnlyTypesThrow()
    {
        Assert.Throws<InvalidOperationException>(() => ImageHandlers.For(new ProgramUninstall("name", "pub", null)));
        Assert.Throws<InvalidOperationException>(() => ImageHandlers.For(new PowerScheme("guid")));
        Assert.Throws<InvalidOperationException>(() => ImageHandlers.For(new LaunchAtLogon("cmd")));
    }
}
