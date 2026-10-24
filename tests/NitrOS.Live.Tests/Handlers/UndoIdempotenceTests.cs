using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

/// <summary>
/// Undo must not write when the target already holds its prior state. Every case reads the prior, changes nothing, then makes
/// Windows refuse all writes: an Undo that still tried to write would throw.
/// </summary>
public class UndoIdempotenceTests
{
    private const string Key = @"Software\NitrOS.Tests\Idem";
    private static IActionHandler H(SwitchAction a) => NitrOS.Live.Handlers.Handlers.For(a);

    private static void UndoUnchanged(FakeSystems f, SwitchAction a, Action refuse)
    {
        var live = f.ToLive();
        var prior = H(a).Read(a, live);
        refuse();
        H(a).Undo(a, prior, live); // throws if it writes
    }

    private static void RefuseRegistry(FakeSystems f, params string[] keys)
    {
        foreach (var k in keys) f.Registry.RefusedKeys.Add((LiveHive.CurrentUser, k));
    }

    [Fact]
    public void RegistrySet_UndoSkipsWhenValueAlreadyPrior()
    {
        var f = new FakeSystems();
        f.Registry.SetValue(LiveHive.CurrentUser, Key, "V", new RegValue(RegistryKind.Dword, "0"), RegView.Default);
        var a = new RegistrySet("HKCU", Key, "V", RegistryKind.Dword, "1");
        UndoUnchanged(f, a, () => RefuseRegistry(f, Key));
        Assert.Empty(f.Registry.Writes.Skip(1));
    }

    [Fact]
    public void RegistrySet_UndoSkipsWhenValueAlreadyAbsent()
    {
        var f = new FakeSystems();
        var a = new RegistrySet("HKCU", Key, "V", RegistryKind.Dword, "1");
        UndoUnchanged(f, a, () => RefuseRegistry(f, Key));
        Assert.Empty(f.Registry.Writes);
    }

    [Fact]
    public void RegistryDelete_UndoSkipsWhenValueOrTreeStillThere()
    {
        var f = new FakeSystems();
        f.Registry.SetValue(LiveHive.CurrentUser, Key, "V", new RegValue(RegistryKind.String, "x"), RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, Key + @"\Sub", "D", new RegValue(RegistryKind.Dword, "2"), RegView.Default);
        var writes = f.Registry.Writes.Count;
        UndoUnchanged(f, new RegistryDelete("HKCU", Key, "V"), () => RefuseRegistry(f, Key, Key + @"\Sub"));
        UndoUnchanged(f, new RegistryDelete("HKCU", Key), () => { });
        Assert.Equal(writes, f.Registry.Writes.Count);
    }

    [Fact]
    public void LaunchAtLogon_UndoSkipsWhenValueAlreadyPrior()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(@"C:\Tools\app.exe");
        const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        f.Registry.SetValue(LiveHive.CurrentUser, Run, "NitrOS.LaunchAtLogon", new RegValue(RegistryKind.String, "old"), RegView.Default);
        UndoUnchanged(f, new LaunchAtLogon(@"C:\Tools\app.exe"), () => RefuseRegistry(f, Run));
        UndoUnchanged(new FakeSystems { }, new LaunchAtLogon(@"C:\Missing\app.exe"), () => { });
        Assert.Single(f.Registry.Writes);
    }

    [Fact]
    public void ServiceStart_UndoSkipsServicesAlreadyPrior()
    {
        var f = new FakeSystems();
        f.Services.Starts["CDPUserSvc"] = ServiceStartType.Automatic;
        f.Services.Starts["CDPUserSvc_1"] = ServiceStartType.Manual;
        f.Services.Instances["CDPUserSvc"] = ["CDPUserSvc_1"];
        UndoUnchanged(f, new ServiceStart("CDPUserSvc", ServiceStartType.Disabled), () => f.Services.RefuseWrites = true);
        Assert.Empty(f.Services.Writes);
    }

    [Fact]
    public void ServiceStart_UndoWritesOnlyTheChangedOnes()
    {
        var f = new FakeSystems();
        f.Services.Starts["CDPUserSvc"] = ServiceStartType.Automatic;
        f.Services.Starts["CDPUserSvc_1"] = ServiceStartType.Manual;
        f.Services.Instances["CDPUserSvc"] = ["CDPUserSvc_1"];
        var a = new ServiceStart("CDPUserSvc", ServiceStartType.Disabled);
        var prior = H(a).Read(a, f.ToLive());
        f.Services.Starts["CDPUserSvc_1"] = ServiceStartType.Disabled;
        f.Services.Writes.Clear();
        H(a).Undo(a, prior, f.ToLive());
        Assert.Equal(["CDPUserSvc_1"], f.Services.Writes);
        Assert.Equal(ServiceStartType.Manual, f.Services.Starts["CDPUserSvc_1"]);
    }

    [Fact]
    public void TaskDisable_UndoSkipsWhenStateAlreadyPrior()
    {
        var f = new FakeSystems();
        f.Tasks.Enabled[@"\T"] = true;
        UndoUnchanged(f, new TaskDisable(@"\T"), () => f.Tasks.RefuseWrites = true);
        Assert.Empty(f.Tasks.Writes);
    }

    [Fact]
    public void PowerSet_UndoSkipsWhenValuesAlreadyPrior()
    {
        var f = new FakeSystems();
        f.Power.Settings[(f.Power.Active, "sub", "set")] = (5, 7);
        UndoUnchanged(f, new PowerSet("sub", "set", 0, 0), () => f.Power.RefuseWrites = true);
        Assert.Empty(f.Power.Writes);
    }

    [Fact]
    public void PowerScheme_UndoSkipsWhenSchemeAlreadyActive()
    {
        var f = new FakeSystems();
        const string Other = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        f.Power.SchemeList.Add(Other);
        UndoUnchanged(f, new PowerScheme(Other), () => f.Power.RefuseWrites = true);
        Assert.Empty(f.Power.Writes);
    }

    [Fact]
    public void StartupDisable_UndoSkipsEntriesAlreadyPrior()
    {
        var f = new FakeSystems();
        const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        f.Registry.SetValue(LiveHive.CurrentUser, Run, "Tool", new RegValue(RegistryKind.String, "tool.exe"), RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, Run, "Fresh", new RegValue(RegistryKind.String, "fresh.exe"), RegView.Default);
        f.Registry.SetValue(LiveHive.CurrentUser, Approved, "Tool", new RegValue(RegistryKind.Binary, "020000000000000000000000"), RegView.Default);
        f.Tasks.Enabled[@"\Startup\T"] = true;
        var writes = f.Registry.Writes.Count;

        UndoUnchanged(f, new StartupDisable(StartupSource.Run, "*", "HKCU"), () => RefuseRegistry(f, Approved));
        UndoUnchanged(f, new StartupDisable(StartupSource.Task, @"\Startup\T"), () => f.Tasks.RefuseWrites = true);

        Assert.Equal(writes, f.Registry.Writes.Count);
        Assert.Empty(f.Tasks.Writes);
    }

    [Fact]
    public void FeatureDisable_UndoSkipsWhenAlreadyEnabled()
    {
        var f = new FakeSystems();
        f.Dism.Features["Feat"] = "Enabled";
        UndoUnchanged(f, new FeatureDisable("Feat"), () => { });
        Assert.Empty(f.Dism.Changes);
    }
}
