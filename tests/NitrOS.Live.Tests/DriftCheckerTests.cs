using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests;

[Collection(TuneLockCollection.Name)]
public sealed class DriftCheckerTests : IDisposable
{
    [Fact]
    public void Drift_RefusesJournalOfAnotherUser()
    {
        var journal = Run(Tune.Sw("sw.a", Tune.Set("A")));
        Tune.Put(_f, "A", "0"); // drifted, but the journal is not this account's to check

        var e = Assert.Throws<JournalOwnerException>(() => new DriftChecker(_f.ToLive()).Check(journal, "S-1-5-21-2"));

        Assert.Equal("This journal belongs to another account.", e.Message);
        Assert.Equal(["sw.a"], new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nitros-drift-" + Guid.NewGuid().ToString("N"));
    private readonly FakeSystems _f = new();
    private readonly JournalStore _store;

    public DriftCheckerTests() => _store = new JournalStore(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private RunJournal Run(params Core.Model.SwitchDef[] defs)
    {
        var result = new TuneEngine(_f.ToLive(), _store).Apply(Tune.Select(defs), Tune.Options, null, CancellationToken.None);
        return _store.Load(result.JournalPath);
    }

    [Fact]
    public void Drift_FindsSwitchWindowsTurnedBack()
    {
        var journal = Run(
            Tune.Sw("sw.kept", Tune.Set("A")),
            Tune.Sw("sw.turned", Tune.Set("B"), Tune.Set("C")),
            Tune.Sw("sw.deleted", Tune.Set("D")));
        Assert.Empty(new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);

        Tune.Put(_f, "C", "0");
        _f.Registry.DeleteValue(Systems.LiveHive.LocalMachine, Tune.Key, "D", Systems.RegView.Default);

        Assert.Equal(["sw.turned", "sw.deleted"], new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);
    }

    [Fact]
    public void Drift_IgnoresSkippedAndIrreversible()
    {
        _f.Appx.Installed.Add("Some.App");
        var journal = Run(
            Tune.Sw("sw.app", new AppxRemove("Some.App")),
            Tune.Sw("sw.skipped", new PowerScheme("no-such-scheme"), new AppxRemove("Missing.App")),
            Tune.Sw("sw.script", new Script("run", "undo")),
            Tune.Sw("sw.reg", Tune.Set("A")));
        Assert.Contains(journal.Entries, e => e.Outcome == ActionOutcome.Skipped);

        _f.Appx.Installed.Add("Some.App");   // Windows reinstalled the removed app
        _f.Appx.Installed.Add("Missing.App"); // and a skipped target appeared
        Tune.Put(_f, "A", "1");

        Assert.Empty(new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);
    }

    [Fact]
    public void Drift_PowerSetUsesRecordedScheme()
    {
        const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        const string Other = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        _f.Power.SchemeList.Add(Other);
        _f.Power.Settings[(Balanced, "sub", "set")] = (10, 10);
        _f.Power.Settings[(Other, "sub", "set")] = (50, 50);
        var journal = Run(Tune.Sw("sw.power", new PowerSet("sub", "set", 0, 0)));

        _f.Power.Active = Other; // the user picked another plan later; the tuned plan still holds its value
        var checker = new DriftChecker(_f.ToLive());
        var report = checker.Check(journal, Tune.Options.UserSid);
        Assert.Empty(report.Drifted);
        Assert.Empty(report.Errors);

        _f.Power.Settings[(Balanced, "sub", "set")] = (10, 0); // Windows reset the tuned plan
        Assert.Equal(["sw.power"], checker.Check(journal, Tune.Options.UserSid).Drifted);
    }

    [Fact]
    public void Drift_ReturnsErrorsInReport()
    {
        var journal = Run(Tune.Sw("sw.reg", Tune.Set("A")), Tune.Sw("sw.other", Tune.Set("B")));
        Tune.Put(_f, "B", "0");
        _f.Registry.FailOn = "A";

        var checker = new DriftChecker(_f.ToLive());
        var report = checker.Check(journal, Tune.Options.UserSid);
        Assert.Equal(["sw.other"], report.Drifted);
        var error = Assert.Single(report.Errors);
        Assert.Equal("sw.reg", error.SwitchId);
        Assert.Contains("FakeRegistry failure", error.Error);

        _f.Registry.FailOn = null;
        var clean = checker.Check(journal, Tune.Options.UserSid);
        Assert.Empty(clean.Errors);
    }

    [Fact]
    public void Drift_IgnoresRolledBackEntries()
    {
        var journal = Run(Tune.Sw("sw.reg", Tune.Set("A")));
        new TuneEngine(_f.ToLive(), _store).Undo(_store.List()[0], null, Tune.Options.UserSid);
        var undone = _store.Load(_store.List()[0]);

        Assert.Empty(new DriftChecker(_f.ToLive()).Check(undone, Tune.Options.UserSid).Drifted);
        Assert.Equal(["sw.reg"], new DriftChecker(_f.ToLive()).Check(journal, Tune.Options.UserSid).Drifted);
    }
}
