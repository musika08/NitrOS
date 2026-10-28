namespace NitrOS.Image.Tests.FirstBoot;

using System;
using NitrOS.Core.Model;
using NitrOS.Image.FirstBoot;
using NitrOS.Live.Systems;

public sealed class FirstBootScriptsTests
{
    [Fact]
    public void Scripts_RefuseUnsafeCharacters()
    {
        var plan = new FirstBootPlan();
        // Item with % in data
        plan.AddMachine(new MachineRegSet("sw1", "HKLM\\SOFTWARE\\Test", "Val", new RegValue(RegistryKind.String, "has%percent")));
        // Item with & in key
        plan.AddMachine(new MachineRegSet("sw2", "HKLM\\SOFTWARE\\Test&Amp", "Val", new RegValue(RegistryKind.Dword, "1")));
        // Safe item
        plan.AddMachine(new TaskOff("sw3", "\\Safe\\Task"));

        var rendered = FirstBootScripts.Render(plan);

        Assert.Equal(2, rendered.Refused.Count);
        Assert.Contains(rendered.Refused, r => r.SwitchId == "sw1");
        Assert.Contains(rendered.Refused, r => r.SwitchId == "sw2");

        Assert.NotNull(rendered.MachineCmd);
        Assert.Contains("Safe\\Task", rendered.MachineCmd);
        Assert.DoesNotContain("has%percent", rendered.MachineCmd);
    }

    [Fact]
    public void Machine_RunsOnceWithMarker()
    {
        var plan = new FirstBootPlan();
        plan.AddMachine(new TaskOff("sw1", "\\Path\\Task"));

        var rendered = FirstBootScripts.Render(plan);
        Assert.NotNull(rendered.MachineCmd);

        Assert.Contains("if exist \"%N%\\machine.done\" exit /b 0", rendered.MachineCmd);
        Assert.Contains("echo done > \"%N%\\machine.done\"", rendered.MachineCmd);
    }

    [Fact]
    public void Machine_NeverRestarts()
    {
        var plan = new FirstBootPlan();
        plan.AddMachine(new TaskOff("sw1", "\\Path\\Task"));
        plan.AddMachine(new ScriptRun("sw2", "Write-Host 'hello'"));

        var rendered = FirstBootScripts.Render(plan);
        Assert.NotNull(rendered.MachineCmd);
        Assert.DoesNotContain("shutdown", rendered.MachineCmd, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Machine_LogsEveryCommandWithSwitchId()
    {
        var plan = new FirstBootPlan();
        plan.AddMachine(new TaskOff("sw_telemetry", "\\Microsoft\\Windows\\CEIP"));

        var rendered = FirstBootScripts.Render(plan);
        Assert.NotNull(rendered.MachineCmd);
        Assert.Contains("sw_telemetry 0 exit %errorlevel% >> \"%N%\\machine.log\"", rendered.MachineCmd);
    }

    [Fact]
    public void User_OnlyHkcuItems_And_Machine_OnlyHklmAndOtherItems()
    {
        var plan = new FirstBootPlan();
        plan.AddMachine(new MachineRegSet("sw_mach", "HKLM\\SOFTWARE\\Test", "Val", new RegValue(RegistryKind.Dword, "1")));
        plan.AddUser(new UserRegSet("sw_user", "HKCU\\Software\\Test", "Val", new RegValue(RegistryKind.Dword, "2")));

        var rendered = FirstBootScripts.Render(plan);
        Assert.NotNull(rendered.MachineCmd);
        Assert.NotNull(rendered.UserCmd);

        Assert.Contains("HKLM\\SOFTWARE\\Test", rendered.MachineCmd);
        Assert.DoesNotContain("HKCU", rendered.MachineCmd);

        Assert.Contains("HKCU\\Software\\Test", rendered.UserCmd);
        Assert.DoesNotContain("HKLM", rendered.UserCmd);
    }

    [Fact]
    public void Render_NothingDeferredGivesNoScripts()
    {
        var plan = new FirstBootPlan();
        var rendered = FirstBootScripts.Render(plan);

        Assert.Null(rendered.MachineCmd);
        Assert.Null(rendered.UserCmd);
        Assert.Empty(rendered.ScriptFiles);
        Assert.Empty(rendered.Refused);
    }
}
