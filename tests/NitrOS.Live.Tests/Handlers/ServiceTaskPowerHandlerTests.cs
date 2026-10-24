using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class ServiceTaskPowerHandlerTests
{
    private static IActionHandler H(SwitchAction a) => NitrOS.Live.Handlers.Handlers.For(a);

    // service.start
    private static readonly ServiceStart Svc = new("DiagTrack", ServiceStartType.Disabled);
    private static readonly ServiceStart Tmpl = new("CDPUserSvc", ServiceStartType.Disabled);

    private static FakeSystems WithTemplate()
    {
        var f = new FakeSystems();
        f.Services.Starts["CDPUserSvc"] = ServiceStartType.Automatic;
        f.Services.Starts["CDPUserSvc_1a2b"] = ServiceStartType.Automatic;
        f.Services.Starts["CDPUserSvc_3c4d"] = ServiceStartType.Manual;
        f.Services.Instances["CDPUserSvc"] = ["CDPUserSvc_1a2b", "CDPUserSvc_3c4d"];
        return f;
    }

    [Fact]
    public void ServiceStart_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Services.Starts["DiagTrack"] = ServiceStartType.Automatic;
        var p = Assert.IsType<ServicePrior>(H(Svc).Read(Svc, f.ToLive()));
        Assert.Equal([new ServiceStartEntry("DiagTrack", ServiceStartType.Automatic)], p.Services);
    }

    [Fact]
    public void ServiceStart_ReadsPriorOfTemplateAndInstances()
    {
        var f = WithTemplate();
        var p = Assert.IsType<ServicePrior>(H(Tmpl).Read(Tmpl, f.ToLive()));
        Assert.Equal(3, p.Services.Count);
        Assert.Contains(new ServiceStartEntry("CDPUserSvc_3c4d", ServiceStartType.Manual), p.Services);
    }

    [Fact]
    public void ServiceStart_Applies()
    {
        var f = new FakeSystems();
        f.Services.Starts["DiagTrack"] = ServiceStartType.Automatic;
        Assert.Equal(ApplyResult.Applied, H(Svc).Apply(Svc, f.ToLive()));
        Assert.Equal(ServiceStartType.Disabled, f.Services.Starts["DiagTrack"]);
    }

    [Fact]
    public void ServiceStart_AppliesToEveryPerUserInstance()
    {
        var f = WithTemplate();
        Assert.Equal(ApplyResult.Applied, H(Tmpl).Apply(Tmpl, f.ToLive()));
        Assert.All(f.Services.Starts.Values, v => Assert.Equal(ServiceStartType.Disabled, v));
    }

    [Fact]
    public void ServiceStart_UndoRestoresPrior()
    {
        var f = WithTemplate();
        var live = f.ToLive();
        var prior = H(Tmpl).Read(Tmpl, live);
        H(Tmpl).Apply(Tmpl, live);
        H(Tmpl).Undo(Tmpl, prior, live);
        Assert.Equal(ServiceStartType.Automatic, f.Services.Starts["CDPUserSvc"]);
        Assert.Equal(ServiceStartType.Automatic, f.Services.Starts["CDPUserSvc_1a2b"]);
        Assert.Equal(ServiceStartType.Manual, f.Services.Starts["CDPUserSvc_3c4d"]);
    }

    [Fact]
    public void ServiceStart_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Svc).Read(Svc, live));
        var r = H(Svc).Apply(Svc, live);
        Assert.Equal(ActionOutcome.Skipped, r.Outcome);
        Assert.Empty(f.Services.Starts);
        H(Svc).Undo(Svc, new MissingPrior(), live);
    }

    [Fact]
    public void ServiceStart_IsInEffectAfterApply()
    {
        var f = WithTemplate();
        var live = f.ToLive();
        Assert.False(H(Tmpl).IsInEffect(Tmpl, live));
        H(Tmpl).Apply(Tmpl, live);
        Assert.True(H(Tmpl).IsInEffect(Tmpl, live));
        f.Services.Starts["CDPUserSvc_3c4d"] = ServiceStartType.Manual;
        Assert.False(H(Tmpl).IsInEffect(Tmpl, live));
    }

    // task.disable
    private static readonly TaskDisable Task = new("\\Microsoft\\Windows\\Foo\\Bar");

    [Fact]
    public void TaskDisable_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Tasks.Enabled[Task.Path] = true;
        Assert.Equal(new TaskPrior(true), H(Task).Read(Task, f.ToLive()));
        f.Tasks.Enabled[Task.Path] = false;
        Assert.Equal(new TaskPrior(false), H(Task).Read(Task, f.ToLive()));
    }

    [Fact]
    public void TaskDisable_Applies()
    {
        var f = new FakeSystems();
        f.Tasks.Enabled[Task.Path] = true;
        Assert.Equal(ApplyResult.Applied, H(Task).Apply(Task, f.ToLive()));
        Assert.False(f.Tasks.Enabled[Task.Path]);
    }

    [Fact]
    public void TaskDisable_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        f.Tasks.Enabled[Task.Path] = true;
        var prior = H(Task).Read(Task, live);
        H(Task).Apply(Task, live);
        H(Task).Undo(Task, prior, live);
        Assert.True(f.Tasks.Enabled[Task.Path]);
        // A task that was already disabled stays disabled on undo.
        f.Tasks.Enabled[Task.Path] = false;
        var prior2 = H(Task).Read(Task, live);
        H(Task).Apply(Task, live);
        H(Task).Undo(Task, prior2, live);
        Assert.False(f.Tasks.Enabled[Task.Path]);
    }

    [Fact]
    public void TaskDisable_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Task).Read(Task, live));
        Assert.Equal(ActionOutcome.Skipped, H(Task).Apply(Task, live).Outcome);
        H(Task).Undo(Task, new MissingPrior(), live);
        Assert.Empty(f.Tasks.Enabled);
    }

    [Fact]
    public void TaskDisable_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.False(H(Task).IsInEffect(Task, live));
        f.Tasks.Enabled[Task.Path] = true;
        Assert.False(H(Task).IsInEffect(Task, live));
        H(Task).Apply(Task, live);
        Assert.True(H(Task).IsInEffect(Task, live));
    }

    // power.set
    private const string Sub = "54533251-82be-4824-96c1-47b60b740d00";
    private const string Set = "be337238-0d82-4146-a960-4f3749d470c7";
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string Other = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private static readonly PowerSet Pw = new(Sub, Set, 100, 50);

    [Fact]
    public void PowerSet_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        Assert.Equal(new PowerPrior(Balanced, 60, 30), H(Pw).Read(Pw, f.ToLive()));
        var acOnly = new PowerSet(Sub, Set, 100);
        Assert.Equal(new PowerPrior(Balanced, 60, null), H(acOnly).Read(acOnly, f.ToLive()));
    }

    [Fact]
    public void PowerSet_Applies()
    {
        var f = new FakeSystems();
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        Assert.Equal(ApplyResult.Applied, H(Pw).Apply(Pw, f.ToLive()));
        Assert.Equal((100, 50), f.Power.Settings[(Balanced, Sub, Set)]);
    }

    [Fact]
    public void PowerSet_AppliesToSchemeActiveAtApplyTime()
    {
        var f = new FakeSystems();
        f.Power.SchemeList.Add(Other);
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        f.Power.Settings[(Other, Sub, Set)] = (10, 20);
        f.Power.Active = Other;
        H(Pw).Apply(Pw, f.ToLive());
        Assert.Equal((100, 50), f.Power.Settings[(Other, Sub, Set)]);
        Assert.Equal((60, 30), f.Power.Settings[(Balanced, Sub, Set)]);
    }

    [Fact]
    public void PowerSet_AcNullLeavesAcUntouched_AndUndoRestoresOnlyWhatWasWritten()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        var dcOnly = new PowerSet(Sub, Set, null, 5);
        var prior = H(dcOnly).Read(dcOnly, live);
        H(dcOnly).Apply(dcOnly, live);
        Assert.Equal((60, 5), f.Power.Settings[(Balanced, Sub, Set)]);
        f.Power.Settings[(Balanced, Sub, Set)] = (77, 5); // AC changed by someone else meanwhile
        H(dcOnly).Undo(dcOnly, prior, live);
        Assert.Equal((77, 30), f.Power.Settings[(Balanced, Sub, Set)]);
    }

    [Fact]
    public void PowerSet_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        var prior = H(Pw).Read(Pw, live);
        H(Pw).Apply(Pw, live);
        H(Pw).Undo(Pw, prior, live);
        Assert.Equal((60, 30), f.Power.Settings[(Balanced, Sub, Set)]);
    }

    [Fact]
    public void PowerSet_UndoRestoresTheRecordedSchemeEvenIfActiveChanged()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        f.Power.SchemeList.Add(Other);
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        f.Power.Settings[(Other, Sub, Set)] = (10, 20);
        var prior = H(Pw).Read(Pw, live);
        H(Pw).Apply(Pw, live);
        f.Power.Active = Other;
        H(Pw).Undo(Pw, prior, live);
        Assert.Equal((60, 30), f.Power.Settings[(Balanced, Sub, Set)]);
        Assert.Equal((10, 20), f.Power.Settings[(Other, Sub, Set)]);
    }

    [Fact]
    public void PowerSet_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Pw).Read(Pw, live));
        Assert.Equal(ActionOutcome.Skipped, H(Pw).Apply(Pw, live).Outcome);
        Assert.Empty(f.Power.Settings);
        H(Pw).Undo(Pw, new MissingPrior(), live);
    }

    [Fact]
    public void PowerSet_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        f.Power.Settings[(Balanced, Sub, Set)] = (60, 30);
        Assert.False(H(Pw).IsInEffect(Pw, live));
        H(Pw).Apply(Pw, live);
        Assert.True(H(Pw).IsInEffect(Pw, live));
        var acOnly = new PowerSet(Sub, Set, 100);
        Assert.True(H(acOnly).IsInEffect(acOnly, live));
    }

    // power.scheme
    private static readonly PowerScheme Sch = new(Other);

    private static FakeSystems WithOtherScheme()
    {
        var f = new FakeSystems();
        f.Power.SchemeList.Add(Other);
        return f;
    }

    [Fact]
    public void PowerScheme_ReadsPrior()
    {
        var f = WithOtherScheme();
        Assert.Equal(new SchemePrior(Balanced), H(Sch).Read(Sch, f.ToLive()));
    }

    [Fact]
    public void PowerScheme_Applies()
    {
        var f = WithOtherScheme();
        Assert.Equal(ApplyResult.Applied, H(Sch).Apply(Sch, f.ToLive()));
        Assert.Equal(Other, f.Power.Active);
    }

    [Fact]
    public void PowerScheme_UndoRestoresPrior()
    {
        var f = WithOtherScheme();
        var live = f.ToLive();
        var prior = H(Sch).Read(Sch, live);
        H(Sch).Apply(Sch, live);
        H(Sch).Undo(Sch, prior, live);
        Assert.Equal(Balanced, f.Power.Active);
    }

    [Fact]
    public void PowerScheme_MissingTargetIsSkippedAndNeverCreated()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Sch).Read(Sch, live));
        var r = H(Sch).Apply(Sch, live);
        Assert.Equal(ActionOutcome.Skipped, r.Outcome);
        Assert.Equal("not available on this PC", r.Detail);
        Assert.Equal([Balanced], f.Power.SchemeList);
        Assert.Equal(Balanced, f.Power.Active);
        H(Sch).Undo(Sch, new MissingPrior(), live);
    }

    [Fact]
    public void PowerScheme_IsInEffectAfterApply()
    {
        var f = WithOtherScheme();
        var live = f.ToLive();
        Assert.False(H(Sch).IsInEffect(Sch, live));
        H(Sch).Apply(Sch, live);
        Assert.True(H(Sch).IsInEffect(Sch, live));
    }
}
