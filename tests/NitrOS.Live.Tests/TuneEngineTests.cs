using NitrOS.Core.Model;
using NitrOS.Core.Selecting;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests;

/// <summary>Builds switches, selections and registry actions for the engine and drift tests.</summary>
internal static class Tune
{
    public const string Key = @"SOFTWARE\NitrOS\Tune";
    public static readonly TuneOptions Options = new(false, "S-1-5-21-1", "26100");

    public static RegistrySet Set(string name, string data = "1") => new("HKLM", Key, name, RegistryKind.Dword, data);

    public static RegValue? Get(FakeSystems f, string name) =>
        f.Registry.GetValue(LiveHive.LocalMachine, Key, name, RegView.Default);

    public static void Put(FakeSystems f, string name, string data) =>
        f.Registry.SetValue(LiveHive.LocalMachine, Key, name, new RegValue(RegistryKind.Dword, data), RegView.Default);

    public static RegValue Dword(string data) => new(RegistryKind.Dword, data);

    public static SwitchDef Sw(string id, params SwitchAction[] live) => new()
    {
        Id = id,
        Name = id,
        Feature = "Test feature",
        Description = "Test switch " + id,
        Risk = Risk.Safe,
        Modes = [Mode.Live],
        Reversible = true,
        Cards = new CardDefaults
        {
            Recommended = CardDefault.Off,
            Gamer = CardDefault.Off,
            Streamer = CardDefault.Off,
            MaxLean = CardDefault.Off,
        },
        Live = live,
    };

    public static Selection Select(params SwitchDef[] defs)
    {
        var catalog = new Catalog([new GroupFile("test", "Test", defs.Select(d => d with { Group = "test" }).ToList())],
            new CardMeasurements());
        return Selection.FromIds(catalog, defs.Select(d => d.Id), TargetMode.TunePc, DeviceProfile.Desktop);
    }
}

/// <summary>
/// Test classes that run the engine share the system-wide run lock (Global\NitrOS.Tune), so they are kept in one collection
/// and never run at the same time as each other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TuneLockCollection
{
    public const string Name = "Tune run lock";
}

[Collection(TuneLockCollection.Name)]
public sealed class TuneEngineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nitros-tune-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSystems _f = new();
    private readonly JournalStore _store;

    public TuneEngineTests() => _store = new JournalStore(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private TuneEngine Engine() => new(_f.ToLive(), _store);

    private TuneResult Apply(Selection sel, TuneOptions? options = null) =>
        Engine().Apply(sel, options ?? Tune.Options, null, CancellationToken.None);

    private sealed class CrashException() : Exception("simulated crash");

    private sealed class CallbackProgress(Action<TuneProgress> onReport) : IProgress<TuneProgress>
    {
        public void Report(TuneProgress value) => onReport(value);
    }

    [Fact]
    public void TuneEngine_IsRunInProgressWhileApplying()
    {
        bool inProgressDuring = false;
        var progress = new CallbackProgress(_ => inProgressDuring = TuneEngine.IsRunInProgress());
        var result = Engine().Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))), Tune.Options, progress, CancellationToken.None);
        Assert.False(result.Refused);
        Assert.True(inProgressDuring);
        Assert.False(TuneEngine.IsRunInProgress());
    }

    [Fact]
    public void Apply_RollsBackSwitchWhenAnActionFails()
    {
        Tune.Put(_f, "A", "0");
        _f.Registry.FailOn = "B"; // reading or writing value B throws
        var thrower = Tune.Sw("sw.throws", Tune.Set("A"), Tune.Set("B"));
        var fine = Tune.Sw("sw.fine", Tune.Set("C"));
        var failing = Tune.Sw("sw.fails", Tune.Set("D"), new Script("run", "undo"));
        _f.Scripts.ExitCode = 1; // the script reports failure without throwing

        var result = Apply(Tune.Select(thrower, fine, failing));

        Assert.False(result.Refused);
        Assert.Equal(SwitchResult.Failed, result.Switches["sw.throws"]);
        Assert.Equal(SwitchResult.Applied, result.Switches["sw.fine"]);
        Assert.Equal(SwitchResult.Failed, result.Switches["sw.fails"]);
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "C"));
        Assert.Null(Tune.Get(_f, "D"));

        var journal = _store.Load(result.JournalPath);
        var byKey = journal.Entries.ToDictionary(e => (e.SwitchId, e.ActionIndex));
        Assert.Equal(ActionOutcome.RolledBack, byKey[("sw.throws", 0)].Outcome);
        Assert.Equal(ActionOutcome.Failed, byKey[("sw.throws", 1)].Outcome);
        Assert.Equal(ActionOutcome.Applied, byKey[("sw.fine", 0)].Outcome);
        Assert.Equal(ActionOutcome.RolledBack, byKey[("sw.fails", 0)].Outcome);
        Assert.Equal(ActionOutcome.Failed, byKey[("sw.fails", 1)].Outcome);
        Assert.Contains("exit code 1", byKey[("sw.fails", 1)].Detail);
        Assert.DoesNotContain("undo", _f.Scripts.Bodies); // a failed script's undo body never runs
        Assert.DoesNotContain("sw.throws", result.MayBeChanged); // its read failed before anything ran
        Assert.Contains("sw.fails", result.MayBeChanged); // the script ran and may have changed part of the PC
    }

    [Fact]
    public void Apply_RollsBackWhenApplyThrows()
    {
        Tune.Put(_f, "A", "0");
        _f.Scripts.FailOn = "boom"; // Read works, Apply throws
        var sw = Tune.Sw("sw.x", Tune.Set("A"), new Script("boom", "undo"));

        var result = Apply(Tune.Select(sw));

        Assert.Equal(SwitchResult.Failed, result.Switches["sw.x"]);
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
        Assert.DoesNotContain("undo", _f.Scripts.Bodies);
        var entries = _store.Load(result.JournalPath).Entries;
        Assert.Equal(ActionOutcome.RolledBack, entries[0].Outcome);
        Assert.Equal(ActionOutcome.Failed, entries[1].Outcome);
        Assert.Contains("FakeScripts failure", entries[1].Detail);
        Assert.Equal(["sw.x"], result.MayBeChanged); // the script's Apply ran, so it may have changed something
    }

    [Fact]
    public void Apply_FailedActionRollbackThrows_LeavesEntryUndoable()
    {
        Tune.Put(_f, "A", "0");
        Tune.Put(_f, "B", "0");
        // Once B's prior is read and its pending entry saved, make every touch of B throw: Apply fails and so does its rollback.
        var engine = new TuneEngine(_f.ToLive(), _store, j =>
        {
            if (j.Entries.Count > 0 && j.Entries[^1].ActionIndex == 1 && j.Entries[^1].Detail == TuneEngine.PendingDetail)
                _f.Registry.FailOn = "B";
            return _store.Save(j);
        });
        var result = engine.Apply(Tune.Select(Tune.Sw("sw.x", Tune.Set("A"), Tune.Set("B"))), Tune.Options, null, CancellationToken.None);

        Assert.Equal(SwitchResult.Failed, result.Switches["sw.x"]);
        Assert.Equal(["sw.x"], result.MayBeChanged);
        var entries = _store.Load(result.JournalPath).Entries;
        Assert.Equal(ActionOutcome.RolledBack, entries[0].Outcome);
        Assert.Equal(ActionOutcome.Applied, entries[1].Outcome);
        Assert.StartsWith("Rollback failed: ", entries[1].Detail);

        _f.Registry.FailOn = null;
        Tune.Put(_f, "B", "1"); // pretend the failed Apply half-landed
        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);
        Assert.Equal(["sw.x"], undo.Undone);
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "B"));
    }

    [Fact]
    public void Apply_CrashBetweenApplyAndOutcomeSave_UndoRestores()
    {
        Tune.Put(_f, "A", "0");
        var count = 0;
        var engine = new TuneEngine(_f.ToLive(), _store, j =>
        {
            if (++count == 3) throw new CrashException(); // 1 = empty journal, 2 = pending entry, 3 = outcome
            return _store.Save(j);
        });
        Assert.Throws<CrashException>(() =>
            engine.Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))), Tune.Options, null, CancellationToken.None));
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));

        var path = Assert.Single(_store.List());
        Assert.Equal(TuneEngine.PendingDetail, Assert.Single(_store.Load(path).Entries).Detail);
        var undo = Engine().Undo(path, null, Tune.Options.UserSid);

        Assert.Equal(["sw.a"], undo.Undone);
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
    }

    [Fact]
    public void Apply_CancelMidSwitchRollsBackThatSwitch()
    {
        Tune.Put(_f, "A", "0");
        using var cts = new CancellationTokenSource();
        var progress = new CallbackProgress(p => { if (p.Done == 1) cts.Cancel(); });

        var result = Engine().Apply(Tune.Select(Tune.Sw("sw.two", Tune.Set("A"), Tune.Set("B"))), Tune.Options, progress, cts.Token);

        Assert.Equal(SwitchResult.Failed, result.Switches["sw.two"]);
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
        Assert.Null(Tune.Get(_f, "B"));
        Assert.Empty(result.MayBeChanged);
        var entry = Assert.Single(_store.Load(result.JournalPath).Entries);
        Assert.Equal(ActionOutcome.RolledBack, entry.Outcome);
        Assert.Equal("cancelled", entry.Detail);
    }

    [Fact]
    public void Apply_ProgressCallbackThrows_RunContinues()
    {
        var progress = new CallbackProgress(_ => throw new InvalidOperationException("UI gone"));

        var result = Engine().Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.b", Tune.Set("B"))),
            Tune.Options, progress, CancellationToken.None);

        Assert.Equal(SwitchResult.Applied, result.Switches["sw.a"]);
        Assert.Equal(SwitchResult.Applied, result.Switches["sw.b"]);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "B"));
        Assert.Equal(["sw.a", "sw.b"], result.MayBeChanged);
    }

    [Fact]
    public void Undo_SavesToGivenPath()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))));
        var elsewhere = Path.Combine(_dir, "copy", "moved.json");
        Directory.CreateDirectory(Path.GetDirectoryName(elsewhere)!);
        File.Copy(result.JournalPath, elsewhere);

        Engine().Undo(elsewhere, null, Tune.Options.UserSid);

        Assert.Equal(ActionOutcome.RolledBack, Assert.Single(_store.Load(elsewhere).Entries).Outcome);
        Assert.Equal(ActionOutcome.Applied, Assert.Single(_store.Load(result.JournalPath).Entries).Outcome);
    }

    [Fact]
    public void Undo_Twice_SecondReportsNothingToUndo()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.skip", new AppxRemove("Missing.App"))));

        var first = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);
        Assert.Equal(["sw.a"], first.Undone);
        Assert.Equal(["sw.skip"], first.NothingToUndo);

        var second = Engine().Undo(result.JournalPath, ["sw.a", "sw.unknown"], Tune.Options.UserSid);
        Assert.Empty(second.Undone);
        Assert.Empty(second.CannotUndo);
        Assert.Equal(["sw.a", "sw.unknown"], second.NothingToUndo);
    }

    [Fact]
    public void Apply_PersistsJournalAfterEveryAction()
    {
        var sel = Tune.Select(Tune.Sw("sw.one", Tune.Set("A"), Tune.Set("B")), Tune.Sw("sw.two", Tune.Set("C")));

        // A full run saves once up front, then twice per action (before and after Apply).
        var saves = 0;
        var full = new TuneEngine(_f.ToLive(), _store, j => { saves++; return _store.Save(j); })
            .Apply(sel, Tune.Options, null, CancellationToken.None);
        Assert.Equal(1 + 2 * 3, saves);
        Assert.Equal(3, _store.Load(full.JournalPath).Entries.Count);

        // Crash on the save that would record action N+1, for every N: the file on disk holds exactly N finished entries.
        for (var n = 0; n < 3; n++)
        {
            var f = new FakeSystems();
            var dir = Path.Combine(_dir, "crash" + n);
            var store = new JournalStore(dir);
            var count = 0;
            var crashAt = 2 * n + 2;
            var engine = new TuneEngine(f.ToLive(), store, j =>
            {
                if (++count == crashAt) throw new CrashException();
                return store.Save(j);
            });

            Assert.Throws<CrashException>(() => engine.Apply(sel, Tune.Options, null, CancellationToken.None));

            var journal = store.Load(Assert.Single(store.List()));
            Assert.Equal(n, journal.Entries.Count);
            Assert.All(journal.Entries, e => Assert.Equal(ActionOutcome.Applied, e.Outcome));
            Assert.Equal(["sw.one", "sw.two"], journal.SwitchIds);
            var applied = new[] { "A", "B", "C" }.Count(name => Tune.Get(f, name) is not null);
            Assert.Equal(n, applied);
        }
    }

    [Fact]
    public void Apply_SkipsMissingTargets()
    {
        _f.Appx.Installed.Add("Present.App");
        var allMissing = Tune.Sw("sw.missing", new AppxRemove("Missing.App"), new PowerScheme("no-such-scheme"));
        var partly = Tune.Sw("sw.partly", new AppxRemove("Missing.Too"), Tune.Set("A"));
        var present = Tune.Sw("sw.present", new AppxRemove("Present.App"));

        var result = Apply(Tune.Select(allMissing, partly, present));

        Assert.Equal(SwitchResult.Skipped, result.Switches["sw.missing"]);
        Assert.Equal(SwitchResult.PartlyApplied, result.Switches["sw.partly"]);
        Assert.Equal(SwitchResult.Applied, result.Switches["sw.present"]);
        Assert.Empty(_f.Appx.Installed);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));

        var journal = _store.Load(result.JournalPath);
        var skipped = journal.Entries.Where(e => e.Outcome == ActionOutcome.Skipped).ToList();
        Assert.Equal(3, skipped.Count);
        Assert.All(skipped, e => Assert.IsType<MissingPrior>(e.Prior));

        // Undoing the switch whose targets were all missing changes nothing and keeps its entries as they were.
        var activeBefore = _f.Power.Active;
        var undo = Engine().Undo(result.JournalPath, ["sw.missing"], Tune.Options.UserSid);
        Assert.Empty(undo.Undone);
        Assert.Empty(undo.CannotUndo);
        Assert.Equal(["sw.missing"], undo.NothingToUndo);
        Assert.Empty(_f.Registry.Writes.Skip(1)); // only Apply's own write of A
        Assert.Empty(_f.Power.Writes);
        Assert.Equal(activeBefore, _f.Power.Active);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
        Assert.Empty(_f.Appx.Installed);
        Assert.Equal(journal.Entries, _store.Load(result.JournalPath).Entries);
    }

    [Fact]
    public void Undo_RestoresOldValueEvenAfterDrift()
    {
        Tune.Put(_f, "A", "0");
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.b", Tune.Set("B"))));
        Tune.Put(_f, "A", "7"); // Windows changed it again after the run

        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);

        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
        Assert.Null(Tune.Get(_f, "B"));
        Assert.Equal(["sw.b", "sw.a"], undo.Undone);
        Assert.Empty(undo.CannotUndo);
        var journal = _store.Load(result.JournalPath);
        Assert.All(journal.Entries, e => Assert.Equal(ActionOutcome.RolledBack, e.Outcome));
    }

    [Fact]
    public void Undo_OnlyTouchesTheChosenSwitches()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.b", Tune.Set("B"))));

        var undo = Engine().Undo(result.JournalPath, ["sw.b"], Tune.Options.UserSid);

        Assert.Equal(["sw.b"], undo.Undone);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
        Assert.Null(Tune.Get(_f, "B"));
        var journal = _store.Load(result.JournalPath);
        Assert.Equal(ActionOutcome.Applied, journal.Entries.Single(e => e.SwitchId == "sw.a").Outcome);
        Assert.Equal(ActionOutcome.RolledBack, journal.Entries.Single(e => e.SwitchId == "sw.b").Outcome);
    }

    [Fact]
    public void Undo_ReportsIrreversible()
    {
        _f.Appx.Installed.Add("Some.App");
        Tune.Put(_f, "A", "0");
        var mixed = Tune.Sw("sw.mixed", Tune.Set("A"), new AppxRemove("Some.App"), new AppxRemove("Not.Installed"));
        var plain = Tune.Sw("sw.plain", Tune.Set("B"));
        var result = Apply(Tune.Select(mixed, plain));

        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);

        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A"));
        Assert.Null(Tune.Get(_f, "B"));
        Assert.Equal(["sw.plain"], undo.Undone);
        var cannot = Assert.Single(undo.CannotUndo);
        Assert.Equal(("sw.mixed", "Reinstall from the Microsoft Store"), cannot);
        Assert.Empty(_f.Appx.Installed);

        var entries = _store.Load(result.JournalPath).Entries.Where(e => e.SwitchId == "sw.mixed").ToList();
        Assert.Equal(ActionOutcome.RolledBack, entries[0].Outcome);
        Assert.Equal(ActionOutcome.Applied, entries[1].Outcome);
        Assert.Equal(ActionOutcome.Skipped, entries[2].Outcome);
    }

    [Fact]
    public void Apply_RefusesWithoutRestorePointUnlessAllowed()
    {
        var sel = Tune.Select(Tune.Sw("sw.a", Tune.Set("A")));
        _f.RestorePoint.Outcome = RestorePointOutcome.Unavailable;

        var refused = Apply(sel);
        Assert.True(refused.Refused);
        Assert.Equal("System Restore is off on this PC.", refused.RefusedReason);
        Assert.Equal(RestorePointOutcome.Unavailable, refused.RestorePoint);
        Assert.Empty(refused.Switches);
        Assert.Null(Tune.Get(_f, "A"));
        Assert.Empty(_store.List());

        var allowed = Apply(sel, Tune.Options with { AllowWithoutRestorePoint = true });
        Assert.False(allowed.Refused);
        Assert.Null(allowed.RefusedReason);
        Assert.Equal(SwitchResult.Applied, allowed.Switches["sw.a"]);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));

        _f.RestorePoint.Outcome = RestorePointOutcome.SkippedRecent;
        var recent = Apply(Tune.Select(Tune.Sw("sw.b", Tune.Set("B"))));
        Assert.False(recent.Refused);
        Assert.Equal(RestorePointOutcome.SkippedRecent, recent.RestorePoint);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "B"));
        Assert.Equal(3, _f.RestorePoint.Descriptions.Count);
    }

    [Fact]
    public void Apply_OrdersPowerSchemeBeforePowerSet()
    {
        const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        _f.Power.SchemeList.Add(Ultimate);
        _f.Power.Settings[(Balanced, "sub", "set")] = (10, 10);
        _f.Power.Settings[(Ultimate, "sub", "set")] = (20, 20);
        var setter = Tune.Sw("sw.set", new PowerSet("sub", "set", 0, 0));
        var other = Tune.Sw("sw.other", Tune.Set("A"));
        var scheme = Tune.Sw("sw.scheme", new PowerScheme(Ultimate));

        var result = Apply(Tune.Select(setter, other, scheme));

        Assert.Equal(Ultimate, _f.Power.Active);
        Assert.Equal((0, 0), _f.Power.Settings[(Ultimate, "sub", "set")]);
        Assert.Equal((10, 10), _f.Power.Settings[(Balanced, "sub", "set")]);
        var journal = _store.Load(result.JournalPath);
        Assert.Equal(["sw.scheme", "sw.set", "sw.other"], journal.SwitchIds);
        Assert.Equal(["sw.scheme", "sw.set", "sw.other"], journal.Entries.Select(e => e.SwitchId));
        Assert.Equal(new PowerPrior(Ultimate, 20, 20), journal.Entries[1].Prior);
    }

    [Fact]
    public void Apply_CancellationLeavesValidJournal()
    {
        using var cts = new CancellationTokenSource();
        var reports = new List<TuneProgress>();
        var progress = new CallbackProgress(p =>
        {
            reports.Add(p);
            if (p.Done == 1) cts.Cancel();
        });
        var sel = Tune.Select(Tune.Sw("sw.one", Tune.Set("A")), Tune.Sw("sw.two", Tune.Set("B"), Tune.Set("C")));

        var result = Engine().Apply(sel, Tune.Options, progress, cts.Token);

        Assert.False(result.Refused);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
        Assert.Null(Tune.Get(_f, "B"));
        Assert.Equal(new TuneProgress("sw.one", 1, 3), Assert.Single(reports));
        Assert.Equal(SwitchResult.Applied, result.Switches["sw.one"]);
        Assert.False(result.Switches.ContainsKey("sw.two"));

        var journal = _store.Load(result.JournalPath);
        var entry = Assert.Single(journal.Entries);
        Assert.Equal(ActionOutcome.Applied, entry.Outcome);

        // The saved journal is good enough to undo from.
        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);
        Assert.Equal(["sw.one"], undo.Undone);
        Assert.Null(Tune.Get(_f, "A"));
    }

    [Fact]
    public void Apply_NamesJournalsByLocalTimeAndAvoidsClashes()
    {
        var sel = Tune.Select(Tune.Sw("sw.a", Tune.Set("A")));
        var first = Apply(sel);
        var second = Apply(sel);
        var third = Apply(sel);

        var names = new[] { first, second, third }.Select(r => Path.GetFileNameWithoutExtension(r.JournalPath)).ToList();
        Assert.Equal(3, names.Distinct().Count());
        Assert.Matches(@"^\d{8}-\d{6}(-\d+)?$", names[0]);
        Assert.All(names, n => Assert.Matches(@"^\d{8}-\d{6}(-[23])?$", n));
    }

    // ---- I1: failed irreversible actions stay visible ----

    [Fact]
    public void Apply_FailedIrreversibleAction_IsMayBeChangedAndUndoGivesItsHint()
    {
        _f.Appx.Installed.Add("Some.App");
        _f.Appx.Provisioned.Add("Some.App");
        _f.Appx.DeprovisionThrows = new InvalidOperationException("deprovision broke");
        Tune.Put(_f, "A", "0");
        var sw = Tune.Sw("sw.appx", Tune.Set("A"), new AppxRemove("Some.App"));

        var result = Apply(Tune.Select(sw));

        Assert.Equal(SwitchResult.Failed, result.Switches["sw.appx"]);
        Assert.Equal(["sw.appx"], result.MayBeChanged);
        Assert.Empty(_f.Appx.Installed); // the removal for all users ran before the deprovision failed
        Assert.Equal(Tune.Dword("0"), Tune.Get(_f, "A")); // the reversible part was rolled back
        Assert.Contains(result.Problems, p => p.SwitchId == "sw.appx" && p.Detail.Contains("deprovision broke"));

        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);
        Assert.Equal(("sw.appx", "Reinstall from the Microsoft Store"), Assert.Single(undo.CannotUndo));
        Assert.Empty(undo.Undone);
        Assert.Empty(undo.NothingToUndo);
    }

    [Fact]
    public void Undo_FailedScriptIsReportedAsCannotUndo()
    {
        _f.Scripts.ExitCode = 1;
        var result = Apply(Tune.Select(Tune.Sw("sw.script", new Script("run", "undo"))));

        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);

        var (id, reason) = Assert.Single(undo.CannotUndo);
        Assert.Equal("sw.script", id);
        Assert.Contains("script", reason);
        Assert.DoesNotContain("undo", _f.Scripts.Bodies);
    }

    // ---- I2: undo is idempotent ----

    [Fact]
    public void Apply_RefusedBeforeChangingAnything_RollsBackCleanly()
    {
        const string UserKey = @"Software\NitrOS.Tests\Refused";
        _f.Registry.SetValue(LiveHive.CurrentUser, UserKey, "V", Tune.Dword("0"), RegView.Default);
        _f.Registry.RefusedKeys.Add((LiveHive.CurrentUser, UserKey)); // HKCU is not pre-flighted, so Apply meets the refusal
        _f.Services.Starts["Svc"] = ServiceStartType.Automatic;
        _f.Services.RefuseWrites = true;
        var reg = Tune.Sw("sw.reg", new RegistrySet("HKCU", UserKey, "V", RegistryKind.Dword, "1"));
        var svc = Tune.Sw("sw.svc", new ServiceStart("Svc", ServiceStartType.Disabled));

        var result = Apply(Tune.Select(reg, svc));

        Assert.Equal(SwitchResult.Failed, result.Switches["sw.reg"]);
        Assert.Equal(SwitchResult.Failed, result.Switches["sw.svc"]);
        Assert.Empty(result.MayBeChanged);
        var journal = _store.Load(result.JournalPath);
        Assert.All(journal.Entries, e =>
        {
            Assert.Equal(ActionOutcome.Failed, e.Outcome);
            Assert.DoesNotContain("Rollback failed", e.Detail);
        });
        Assert.Empty(new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);
        var undo = Engine().Undo(result.JournalPath, null, Tune.Options.UserSid);
        Assert.Empty(undo.CannotUndo);
        Assert.Equal(Tune.Dword("0"), _f.Registry.GetValue(LiveHive.CurrentUser, UserKey, "V", RegView.Default));
        Assert.Equal(ServiceStartType.Automatic, _f.Services.Starts["Svc"]);
    }

    // ---- I3: a journal belongs to one account ----

    [Fact]
    public void Undo_RefusesJournalOfAnotherUser()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))));
        var before = File.ReadAllText(result.JournalPath);

        var e = Assert.Throws<JournalOwnerException>(() => Engine().Undo(result.JournalPath, null, "S-1-5-21-2"));

        Assert.Equal("This journal belongs to another account.", e.Message);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
        Assert.Equal(before, File.ReadAllText(result.JournalPath));
        Assert.Equal(["sw.a"], Engine().Undo(result.JournalPath, null, "s-1-5-21-1").Undone); // SIDs compare case-insensitively
    }

    // ---- I4: pre-flight for refused registry writes ----

    [Fact]
    public void Apply_SkipsWholeSwitchWhenWindowsRefusesItsHklmKey()
    {
        _f.Registry.RefusedKeys.Add((LiveHive.LocalMachine, Tune.Key));
        _f.Services.Starts["Svc"] = ServiceStartType.Automatic;
        var refused = Tune.Sw("sw.refused", new ServiceStart("Svc", ServiceStartType.Disabled), Tune.Set("A"));
        var fine = Tune.Sw("sw.fine", new RegistrySet("HKCU", @"Software\NitrOS.Tests\Fine", "V", RegistryKind.Dword, "1"));

        var result = Apply(Tune.Select(refused, fine));

        var detail = $@"Windows refuses changes to HKLM\{Tune.Key} on this PC";
        Assert.Equal(SwitchResult.Skipped, result.Switches["sw.refused"]);
        Assert.Equal(SwitchResult.Applied, result.Switches["sw.fine"]);
        Assert.Empty(_f.Services.Writes); // no action of the refused switch ran
        Assert.Equal(ServiceStartType.Automatic, _f.Services.Starts["Svc"]);
        Assert.Null(Tune.Get(_f, "A"));
        Assert.Equal(["sw.fine"], result.MayBeChanged);
        Assert.Contains(("sw.refused", detail), result.Problems);
        var mine = _store.Load(result.JournalPath).Entries.Where(e => e.SwitchId == "sw.refused").ToList();
        Assert.Equal(2, mine.Count);
        Assert.All(mine, e =>
        {
            Assert.Equal(ActionOutcome.Skipped, e.Outcome);
            Assert.Equal(detail, e.Detail);
        });
    }

    [Fact]
    public void Apply_PreflightTestsNearestExistingParentAndDeleteAndStartupKeys()
    {
        const string Parent = @"SOFTWARE\NitrOS";
        _f.Registry.SetValue(LiveHive.LocalMachine, Parent, "Marker", Tune.Dword("1"), RegView.Default);
        _f.Registry.RefusedKeys.Add((LiveHive.LocalMachine, Parent));
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        _f.Registry.SetValue(LiveHive.LocalMachine, Approved, "Other", new RegValue(RegistryKind.Binary, "020000000000000000000000"), RegView.Default);
        _f.Registry.RefusedKeys.Add((LiveHive.LocalMachine, Approved));
        _f.Registry.SetValue(LiveHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "Tool", new RegValue(RegistryKind.String, "tool.exe"), RegView.Default);

        var missingKey = Tune.Sw("sw.missing-key", Tune.Set("A")); // SOFTWARE\NitrOS\Tune does not exist yet
        var delete = Tune.Sw("sw.delete", new RegistryDelete("HKLM", Parent, "Marker"));
        var startup = Tune.Sw("sw.startup", new StartupDisable(StartupSource.Run, "Tool", "HKLM"));

        var result = Apply(Tune.Select(missingKey, delete, startup));

        Assert.Equal(SwitchResult.Skipped, result.Switches["sw.missing-key"]);
        Assert.Equal(SwitchResult.Skipped, result.Switches["sw.delete"]);
        Assert.Equal(SwitchResult.Skipped, result.Switches["sw.startup"]);
        Assert.Contains(("sw.missing-key", $@"Windows refuses changes to HKLM\{Tune.Key} on this PC"), result.Problems);
        Assert.Contains(("sw.startup", $@"Windows refuses changes to HKLM\{Approved} on this PC"), result.Problems);
        Assert.Equal(Tune.Dword("1"), _f.Registry.GetValue(LiveHive.LocalMachine, Parent, "Marker", RegView.Default));
        Assert.Empty(result.MayBeChanged);
    }

    // ---- M6: one run at a time ----

    [Fact]
    public void Apply_SecondConcurrentRunIsRefused()
    {
        var seed = Apply(Tune.Select(Tune.Sw("sw.seed", Tune.Set("S"))));
        TuneResult? inner = null;
        Exception? innerUndo = null;
        var engine = new TuneEngine(_f.ToLive(), _store, j =>
        {
            if (inner is null)
            {
                // A separate thread (a Task could be inlined on this one), so the mutex this thread holds is not re-entered.
                var other = new Thread(() =>
                {
                    inner = Engine().Apply(Tune.Select(Tune.Sw("sw.b", Tune.Set("B"))), Tune.Options, null, CancellationToken.None);
                    innerUndo = Record.Exception(() => Engine().Undo(seed.JournalPath, null, Tune.Options.UserSid));
                });
                other.Start();
                Assert.True(other.Join(TimeSpan.FromSeconds(30)));
            }
            return _store.Save(j);
        });

        var outer = engine.Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))), Tune.Options, null, CancellationToken.None);

        Assert.True(inner!.Refused);
        Assert.Equal("Another NitrOS change is already running.", inner.RefusedReason);
        Assert.Null(Tune.Get(_f, "B"));
        Assert.IsType<TuneBusyException>(innerUndo);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "S"));
        Assert.False(outer.Refused);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
        Assert.False(Apply(Tune.Select(Tune.Sw("sw.c", Tune.Set("C")))).Refused); // the lock is free again
    }

    // ---- M7 (engine side): an unconfirmed restore point is not "System Restore is off" ----

    [Fact]
    public void Apply_UnconfirmedRestorePointGoesAhead()
    {
        _f.RestorePoint.Outcome = RestorePointOutcome.CreatedUnknown;
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))));
        Assert.False(result.Refused);
        Assert.Equal(RestorePointOutcome.CreatedUnknown, result.RestorePoint);
        Assert.Equal(Tune.Dword("1"), Tune.Get(_f, "A"));
    }
}
