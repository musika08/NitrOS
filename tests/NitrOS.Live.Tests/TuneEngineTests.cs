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
        Assert.Empty(result.LeftChanged.Intersect(["sw.throws", "sw.fails"]));
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
        Assert.Empty(result.LeftChanged);
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
        Assert.Equal(["sw.x"], result.LeftChanged);
        var entries = _store.Load(result.JournalPath).Entries;
        Assert.Equal(ActionOutcome.RolledBack, entries[0].Outcome);
        Assert.Equal(ActionOutcome.Applied, entries[1].Outcome);
        Assert.StartsWith("Rollback failed: ", entries[1].Detail);

        _f.Registry.FailOn = null;
        Tune.Put(_f, "B", "1"); // pretend the failed Apply half-landed
        var undo = Engine().Undo(result.JournalPath, null);
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
        var undo = Engine().Undo(path, null);

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
        Assert.Empty(result.LeftChanged);
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
        Assert.Equal(["sw.a", "sw.b"], result.LeftChanged);
    }

    [Fact]
    public void Undo_SavesToGivenPath()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A"))));
        var elsewhere = Path.Combine(_dir, "copy", "moved.json");
        Directory.CreateDirectory(Path.GetDirectoryName(elsewhere)!);
        File.Copy(result.JournalPath, elsewhere);

        Engine().Undo(elsewhere, null);

        Assert.Equal(ActionOutcome.RolledBack, Assert.Single(_store.Load(elsewhere).Entries).Outcome);
        Assert.Equal(ActionOutcome.Applied, Assert.Single(_store.Load(result.JournalPath).Entries).Outcome);
    }

    [Fact]
    public void Undo_Twice_SecondReportsNothingToUndo()
    {
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.skip", new AppxRemove("Missing.App"))));

        var first = Engine().Undo(result.JournalPath, null);
        Assert.Equal(["sw.a"], first.Undone);
        Assert.Equal(["sw.skip"], first.NothingToUndo);

        var second = Engine().Undo(result.JournalPath, ["sw.a", "sw.unknown"]);
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
    }

    [Fact]
    public void Undo_RestoresOldValueEvenAfterDrift()
    {
        Tune.Put(_f, "A", "0");
        var result = Apply(Tune.Select(Tune.Sw("sw.a", Tune.Set("A")), Tune.Sw("sw.b", Tune.Set("B"))));
        Tune.Put(_f, "A", "7"); // Windows changed it again after the run

        var undo = Engine().Undo(result.JournalPath, null);

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

        var undo = Engine().Undo(result.JournalPath, ["sw.b"]);

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

        var undo = Engine().Undo(result.JournalPath, null);

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
        var undo = Engine().Undo(result.JournalPath, null);
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
}
