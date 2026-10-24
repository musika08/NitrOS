using NitrOS.Core.Model;
using NitrOS.Live.Handlers;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests.Handlers;

public class AppFeatureFileProgramScriptHandlerTests
{
    private static IActionHandler H(SwitchAction a) => NitrOS.Live.Handlers.Handlers.For(a);

    // appx.remove
    private static readonly AppxRemove Appx = new("Microsoft.BingNews");

    [Fact]
    public void AppxRemove_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        var p = Assert.IsType<IrreversiblePrior>(H(Appx).Read(Appx, f.ToLive()));
        Assert.Equal("Microsoft.BingNews", p.What);
        Assert.Equal("Reinstall from the Microsoft Store", p.ReinstallHint);
    }

    [Fact]
    public void AppxRemove_Applies()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.Provisioned.Add("Microsoft.BingNews");
        Assert.Equal(ApplyResult.Applied, H(Appx).Apply(Appx, f.ToLive()));
        Assert.Empty(f.Appx.Installed);
        Assert.Empty(f.Appx.Provisioned);
    }

    [Fact]
    public void AppxRemove_AppliesWildcard()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.Installed.Add("Microsoft.BingWeather");
        var a = new AppxRemove("Microsoft.Bing*");
        Assert.Equal(ApplyResult.Applied, H(a).Apply(a, f.ToLive()));
        Assert.Empty(f.Appx.Installed);
    }

    [Fact]
    public void AppxRemove_UndoDoesNothing()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        var live = f.ToLive();
        var prior = H(Appx).Read(Appx, live);
        H(Appx).Apply(Appx, live);
        H(Appx).Undo(Appx, prior, live);
        Assert.Empty(f.Appx.Installed);
    }

    [Fact]
    public void AppxRemove_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Appx).Read(Appx, live));
        Assert.Equal(ActionOutcome.Skipped, H(Appx).Apply(Appx, live).Outcome);
    }

    [Fact]
    public void AppxRemove_ProvisionedOnlyIsRemoved()
    {
        var f = new FakeSystems();
        f.Appx.Provisioned.Add("Microsoft.BingNews");
        Assert.Equal(ApplyResult.Applied, H(Appx).Apply(Appx, f.ToLive()));
        Assert.Empty(f.Appx.Provisioned);
    }

    [Fact]
    public void AppxRemove_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        var live = f.ToLive();
        Assert.False(H(Appx).IsInEffect(Appx, live));
        H(Appx).Apply(Appx, live);
        Assert.True(H(Appx).IsInEffect(Appx, live));
    }

    // capability.remove
    private static readonly CapabilityRemove Cap = new("App.StepsRecorder~~~~0.0.1.0");

    [Fact]
    public void CapabilityRemove_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        var p = Assert.IsType<IrreversiblePrior>(H(Cap).Read(Cap, f.ToLive()));
        Assert.Equal(Cap.Name, p.What);
    }

    [Fact]
    public void CapabilityRemove_Applies()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        Assert.Equal(ApplyResult.Applied, H(Cap).Apply(Cap, f.ToLive()));
        Assert.Equal("NotPresent", f.Dism.Capabilities[Cap.Name]);
    }

    [Fact]
    public void CapabilityRemove_RebootRequiredCountsAsApplied()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        f.Dism.Result = DismOutcome.RebootRequired;
        var r = H(Cap).Apply(Cap, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Contains("restart", r.Detail);
    }

    [Fact]
    public void CapabilityRemove_FailureIsFailed()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        f.Dism.Result = DismOutcome.Failed;
        Assert.Equal(ActionOutcome.Failed, H(Cap).Apply(Cap, f.ToLive()).Outcome);
    }

    [Fact]
    public void CapabilityRemove_UndoDoesNothing()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        var live = f.ToLive();
        var prior = H(Cap).Read(Cap, live);
        H(Cap).Apply(Cap, live);
        H(Cap).Undo(Cap, prior, live);
        Assert.Equal("NotPresent", f.Dism.Capabilities[Cap.Name]);
    }

    [Fact]
    public void CapabilityRemove_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Cap).Read(Cap, live));
        Assert.Equal(ActionOutcome.Skipped, H(Cap).Apply(Cap, live).Outcome);
        f.Dism.Capabilities[Cap.Name] = "NotPresent";
        Assert.IsType<MissingPrior>(H(Cap).Read(Cap, live));
    }

    [Fact]
    public void CapabilityRemove_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        f.Dism.Capabilities[Cap.Name] = "Installed";
        var live = f.ToLive();
        Assert.False(H(Cap).IsInEffect(Cap, live));
        H(Cap).Apply(Cap, live);
        Assert.True(H(Cap).IsInEffect(Cap, live));
    }

    // feature.disable
    private static readonly FeatureDisable Feat = new("WorkFolders-Client");

    [Fact]
    public void FeatureDisable_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        var p = Assert.IsType<FeaturePrior>(H(Feat).Read(Feat, f.ToLive()));
        Assert.Equal("Enabled", p.State);
    }

    [Fact]
    public void FeatureDisable_Applies()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        Assert.Equal(ApplyResult.Applied, H(Feat).Apply(Feat, f.ToLive()));
        Assert.Equal("Disabled", f.Dism.Features[Feat.Name]);
    }

    [Theory]
    [InlineData(DismOutcome.RebootRequired)]
    [InlineData(DismOutcome.Deferred)]
    public void FeatureDisable_RestartOutcomesCountAsApplied(DismOutcome outcome)
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        f.Dism.Result = outcome;
        var r = H(Feat).Apply(Feat, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Contains("restart", r.Detail);
    }

    [Fact]
    public void FeatureDisable_FailureIsFailed()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        f.Dism.Result = DismOutcome.Failed;
        Assert.Equal(ActionOutcome.Failed, H(Feat).Apply(Feat, f.ToLive()).Outcome);
    }

    [Fact]
    public void FeatureDisable_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        var live = f.ToLive();
        var prior = H(Feat).Read(Feat, live);
        H(Feat).Apply(Feat, live);
        H(Feat).Undo(Feat, prior, live);
        Assert.Equal("Enabled", f.Dism.Features[Feat.Name]);
    }

    [Fact]
    public void FeatureDisable_UndoOfAlreadyDisabledLeavesItDisabled()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Disabled";
        var live = f.ToLive();
        var prior = H(Feat).Read(Feat, live);
        H(Feat).Undo(Feat, prior, live);
        Assert.Equal("Disabled", f.Dism.Features[Feat.Name]);
    }

    [Fact]
    public void FeatureDisable_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Feat).Read(Feat, live));
        Assert.Equal(ActionOutcome.Skipped, H(Feat).Apply(Feat, live).Outcome);
        H(Feat).Undo(Feat, new MissingPrior(), live);
    }

    [Theory]
    [InlineData("Disabled", true)]
    [InlineData("DisablePending", true)]
    [InlineData("DisabledWithPayloadRemoved", true)]
    [InlineData("Enabled", false)]
    [InlineData("EnablePending", false)]
    public void FeatureDisable_IsInEffectForEveryDisabledState(string state, bool expected)
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = state;
        Assert.Equal(expected, H(Feat).IsInEffect(Feat, f.ToLive()));
    }

    [Fact]
    public void FeatureDisable_UndoThrowsWhenReEnablingFails()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        var live = f.ToLive();
        var prior = H(Feat).Read(Feat, live);
        H(Feat).Apply(Feat, live);
        f.Dism.Result = DismOutcome.Failed;
        var e = Assert.Throws<InvalidOperationException>(() => H(Feat).Undo(Feat, prior, live));
        Assert.Contains(Feat.Name, e.Message);
        // A later retry that succeeds puts it back.
        f.Dism.Result = DismOutcome.Done;
        H(Feat).Undo(Feat, prior, live);
        Assert.Equal("Enabled", f.Dism.Features[Feat.Name]);
    }

    [Fact]
    public void FeatureDisable_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = "Enabled";
        var live = f.ToLive();
        Assert.False(H(Feat).IsInEffect(Feat, live));
        H(Feat).Apply(Feat, live);
        Assert.True(H(Feat).IsInEffect(Feat, live));
    }

    // file.remove
    private static readonly FileRemove File = new("C:\\Stuff\\junk.dat");

    [Fact]
    public void FileRemove_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(File.Path);
        var p = Assert.IsType<IrreversiblePrior>(H(File).Read(File, f.ToLive()));
        Assert.Equal(File.Path, p.What);
    }

    [Fact]
    public void FileRemove_Applies()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(File.Path);
        Assert.Equal(ApplyResult.Applied, H(File).Apply(File, f.ToLive()));
        Assert.Equal([File.Path], f.Files.Deleted);
    }

    [Fact]
    public void FileRemove_ExpandsEnvironmentVariables()
    {
        var f = new FakeSystems();
        var expanded = Environment.ExpandEnvironmentVariables("%SystemRoot%\\junk.dat");
        Assert.NotEqual("%SystemRoot%\\junk.dat", expanded);
        f.Files.Existing.Add(expanded);
        var a = new FileRemove("%SystemRoot%\\junk.dat");
        Assert.Equal(ApplyResult.Applied, H(a).Apply(a, f.ToLive()));
        Assert.Equal([expanded], f.Files.Deleted);
    }

    [Fact]
    public void FileRemove_UndoDoesNothing()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(File.Path);
        var live = f.ToLive();
        var prior = H(File).Read(File, live);
        H(File).Apply(File, live);
        H(File).Undo(File, prior, live);
        Assert.DoesNotContain(File.Path, f.Files.Existing);
        Assert.Single(f.Files.Deleted);
    }

    [Fact]
    public void FileRemove_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(File).Read(File, live));
        Assert.Equal(ActionOutcome.Skipped, H(File).Apply(File, live).Outcome);
        Assert.Empty(f.Files.Deleted);
    }

    [Fact]
    public void FileRemove_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        f.Files.Existing.Add(File.Path);
        var live = f.ToLive();
        Assert.False(H(File).IsInEffect(File, live));
        H(File).Apply(File, live);
        Assert.True(H(File).IsInEffect(File, live));
    }

    // program.uninstall
    private static readonly ProgramUninstall Prog = new("Contoso Helper*", "Contoso");

    private static UninstallEntry Entry(string key, string name = "Contoso Helper", string? quiet = "q.exe /s", string? normal = "u.exe") =>
        new(key, name, "Contoso", "1.2.3", quiet, normal);

    [Fact]
    public void ProgramUninstall_ReadsPrior()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        var p = Assert.IsType<IrreversiblePrior>(H(Prog).Read(Prog, f.ToLive()));
        Assert.Equal("Contoso Helper", p.What);
        Assert.Equal("1.2.3", p.Version);
        Assert.Contains("Contoso", p.ReinstallHint);
    }

    [Fact]
    public void ProgramUninstall_Applies()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        Assert.Equal(ApplyResult.Applied, H(Prog).Apply(Prog, f.ToLive()));
        Assert.Single(f.Uninstaller.Runs);
    }

    [Fact]
    public void ProgramUninstall_UndoDoesNothing()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        var live = f.ToLive();
        var prior = H(Prog).Read(Prog, live);
        H(Prog).Apply(Prog, live);
        H(Prog).Undo(Prog, prior, live);
        Assert.Single(f.Uninstaller.Runs);
    }

    [Fact]
    public void ProgramUninstall_MissingTargetIsSkipped()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        Assert.IsType<MissingPrior>(H(Prog).Read(Prog, live));
        Assert.Equal(ActionOutcome.Skipped, H(Prog).Apply(Prog, live).Outcome);
        Assert.Empty(f.Uninstaller.Runs);
    }

    [Fact]
    public void ProgramUninstall_SignatureMismatch_Fails()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        f.Uninstaller.BadSigners.Add("k1");
        var r = H(Prog).Apply(Prog, f.ToLive());
        Assert.Equal(ActionOutcome.Failed, r.Outcome);
        Assert.Contains("signature check failed", r.Detail);
        Assert.Empty(f.Uninstaller.Runs);
    }

    [Fact]
    public void ProgramUninstall_MultipleMatches_RunsEach()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1", "Contoso Helper"));
        f.Uninstaller.Entries.Add(Entry("k2", "Contoso Helper Plus"));
        Assert.Equal(ApplyResult.Applied, H(Prog).Apply(Prog, f.ToLive()));
        Assert.Equal(["k1", "k2"], f.Uninstaller.Runs.Select(r => r.Entry.KeyPath));
    }

    [Fact]
    public void ProgramUninstall_MultipleMatches_OneBadSignerStillRunsTheOthers()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1", "Contoso Helper"));
        f.Uninstaller.Entries.Add(Entry("k2", "Contoso Helper Plus"));
        f.Uninstaller.BadSigners.Add("k1");
        var r = H(Prog).Apply(Prog, f.ToLive());
        Assert.Equal(ActionOutcome.Failed, r.Outcome);
        Assert.Equal(["k2"], f.Uninstaller.Runs.Select(x => x.Entry.KeyPath));
    }

    [Fact]
    public void ProgramUninstall_ExitCode3010_IsApplied()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        f.Uninstaller.ExitCode = 3010;
        var r = H(Prog).Apply(Prog, f.ToLive());
        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Contains("restart", r.Detail);
    }

    [Fact]
    public void ProgramUninstall_OtherExitCode_FailsWithCode()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        f.Uninstaller.ExitCode = 1603;
        var r = H(Prog).Apply(Prog, f.ToLive());
        Assert.Equal(ActionOutcome.Failed, r.Outcome);
        Assert.Contains("1603", r.Detail);
    }

    [Fact]
    public void ProgramUninstall_Args_AppendedAfterSpace()
    {
        // The handler hands the action's args to IUninstaller.Run, which appends them after a space to the
        // quiet (else normal) uninstall string; this checks the hand-over.
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        var a = new ProgramUninstall("Contoso Helper*", "Contoso", "/VERYSILENT");
        H(a).Apply(a, f.ToLive());
        Assert.Equal("/VERYSILENT", f.Uninstaller.Runs[0].Args);
    }

    [Fact]
    public void ProgramUninstall_NoUninstallStringFails()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1", quiet: null, normal: null));
        var r = H(Prog).Apply(Prog, f.ToLive());
        Assert.Equal(ActionOutcome.Failed, r.Outcome);
        Assert.Empty(f.Uninstaller.Runs);
    }

    [Fact]
    public void ProgramUninstall_IsInEffectAfterApply()
    {
        var f = new FakeSystems();
        f.Uninstaller.Entries.Add(Entry("k1"));
        var live = f.ToLive();
        Assert.False(H(Prog).IsInEffect(Prog, live));
        f.Uninstaller.Entries.Clear();
        Assert.True(H(Prog).IsInEffect(Prog, live));
    }

    // script
    private static readonly Script Scr = new("do-it", "undo-it");
    private static readonly Script OneWay = new("do-it");

    [Fact]
    public void Script_ReadsPrior()
    {
        var f = new FakeSystems();
        Assert.IsType<ScriptPrior>(H(Scr).Read(Scr, f.ToLive()));
        Assert.IsType<IrreversiblePrior>(H(OneWay).Read(OneWay, f.ToLive()));
    }

    [Fact]
    public void Script_Applies()
    {
        var f = new FakeSystems();
        Assert.Equal(ApplyResult.Applied, H(Scr).Apply(Scr, f.ToLive()));
        Assert.Equal(["do-it"], f.Scripts.Bodies);
    }

    [Fact]
    public void Script_NonZeroExitFails()
    {
        var f = new FakeSystems();
        f.Scripts.ExitCode = 2;
        var r = H(Scr).Apply(Scr, f.ToLive());
        Assert.Equal(ActionOutcome.Failed, r.Outcome);
        Assert.Contains("2", r.Detail);
    }

    [Fact]
    public void Script_UndoRestoresPrior()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        var prior = H(Scr).Read(Scr, live);
        H(Scr).Apply(Scr, live);
        H(Scr).Undo(Scr, prior, live);
        Assert.Equal(["do-it", "undo-it"], f.Scripts.Bodies);
    }

    [Fact]
    public void Script_WithoutUndoBodyUndoDoesNothing()
    {
        var f = new FakeSystems();
        var live = f.ToLive();
        var prior = H(OneWay).Read(OneWay, live);
        H(OneWay).Apply(OneWay, live);
        H(OneWay).Undo(OneWay, prior, live);
        Assert.Equal(["do-it"], f.Scripts.Bodies);
    }

    [Fact]
    public void Script_IsInEffectAfterApply()
    {
        // A script has no state to inspect, so drift can never be detected: it always counts as in effect.
        var f = new FakeSystems();
        Assert.True(H(Scr).IsInEffect(Scr, f.ToLive()));
    }

    // ---- I4: appx removal when Windows refuses the built-in app list ----

    [Fact]
    public void AppxRemove_ProvisionedQueryRefused_RemovesForUsersAndSkipsDeprovision()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.ProvisionedThrows = new UnauthorizedAccessException("Access is denied. (0x80070005)");
        var live = f.ToLive();

        Assert.IsType<IrreversiblePrior>(H(Appx).Read(Appx, live));
        var r = H(Appx).Apply(Appx, live);

        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Equal(AppxRemoveHandler.ProvisionRefusedDetail, r.Detail);
        Assert.Equal("Windows refused to change the built-in app list", AppxRemoveHandler.ProvisionRefusedDetail);
        Assert.Empty(f.Appx.Installed);
        Assert.Empty(f.Appx.Deprovisioned);
    }

    [Fact]
    public void AppxRemove_DeprovisionAccessDenied_IsAppliedWithDetail()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.Provisioned.Add("Microsoft.BingNews");
        f.Appx.DeprovisionThrows = new InvalidOperationException("Failed deprovisioning X (HRESULT 0x80070005)",
            new UnauthorizedAccessException("Access is denied."));

        var r = H(Appx).Apply(Appx, f.ToLive());

        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Equal(AppxRemoveHandler.ProvisionRefusedDetail, r.Detail);
        Assert.Empty(f.Appx.Installed);
        Assert.Contains("Microsoft.BingNews", f.Appx.Provisioned);
    }

    [Fact]
    public void AppxRemove_DeprovisionAccessDeniedByHResult_IsAppliedWithDetail()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.Provisioned.Add("Microsoft.BingNews");
        f.Appx.DeprovisionThrows = new System.Runtime.InteropServices.COMException("Access is denied.", unchecked((int)0x80070005));

        var r = H(Appx).Apply(Appx, f.ToLive());

        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Equal(AppxRemoveHandler.ProvisionRefusedDetail, r.Detail);
    }

    [Fact]
    public void AppxRemove_ProvisionedOnlyAndRefused_IsSkippedWithDetail()
    {
        var f = new FakeSystems();
        f.Appx.ProvisionedThrows = new UnauthorizedAccessException("Access is denied.");
        var r = H(Appx).Apply(Appx, f.ToLive());
        Assert.Equal(ActionOutcome.Skipped, r.Outcome);
        Assert.Contains(AppxRemoveHandler.ProvisionRefusedDetail, r.Detail);
        Assert.Empty(f.Appx.Removed);
    }

    [Fact]
    public void AppxRemove_OtherDeprovisionErrorStillThrows()
    {
        var f = new FakeSystems();
        f.Appx.Installed.Add("Microsoft.BingNews");
        f.Appx.Provisioned.Add("Microsoft.BingNews");
        f.Appx.DeprovisionThrows = new InvalidOperationException("disk full");
        Assert.Throws<InvalidOperationException>(() => H(Appx).Apply(Appx, f.ToLive()));
    }

    // ---- I5: a later match that an earlier uninstaller already removed ----

    [Fact]
    public void ProgramUninstall_LaterMatchRemovedByEarlierRun_IsSkippedAsAlreadyRemoved()
    {
        var f = new FakeSystems();
        var first = Entry("k1", "Contoso Helper");
        var second = Entry("k2", "Contoso Helper Plus");
        f.Uninstaller.Entries.Add(first);
        f.Uninstaller.Entries.Add(second);
        f.Uninstaller.OnRun = e => { if (e == first) f.Uninstaller.Entries.Remove(second); };

        var r = H(Prog).Apply(Prog, f.ToLive());

        Assert.Equal(ActionOutcome.Applied, r.Outcome);
        Assert.Equal(["k1"], f.Uninstaller.Runs.Select(x => x.Entry.KeyPath));
        Assert.DoesNotContain("signature", r.Detail ?? "");
    }

    // ---- I7: a feature already off is skipped without DISM ----

    [Theory]
    [InlineData("Disabled")]
    [InlineData("DisablePending")]
    [InlineData("DisabledWithPayloadRemoved")]
    public void FeatureDisable_AlreadyOff_IsSkippedWithoutDism(string state)
    {
        var f = new FakeSystems();
        f.Dism.Features[Feat.Name] = state;
        var r = H(Feat).Apply(Feat, f.ToLive());
        Assert.Equal(ActionOutcome.Skipped, r.Outcome);
        Assert.Equal("already off", r.Detail);
        Assert.Empty(f.Dism.Changes);
        Assert.Equal(state, f.Dism.Features[Feat.Name]);
    }
}
