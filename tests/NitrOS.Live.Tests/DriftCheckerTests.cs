using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Tests.Fakes;

namespace NitrOS.Live.Tests;

public sealed class DriftCheckerTests : IDisposable
{
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
        Assert.Empty(new DriftChecker(_f.ToLive()).Check(journal));

        Tune.Put(_f, "C", "0");
        _f.Registry.DeleteValue(Systems.LiveHive.LocalMachine, Tune.Key, "D", Systems.RegView.Default);

        Assert.Equal(["sw.turned", "sw.deleted"], new DriftChecker(_f.ToLive()).Check(journal));
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

        Assert.Empty(new DriftChecker(_f.ToLive()).Check(journal));
    }

    [Fact]
    public void Drift_IgnoresRolledBackEntries()
    {
        var journal = Run(Tune.Sw("sw.reg", Tune.Set("A")));
        new TuneEngine(_f.ToLive(), _store).Undo(_store.List()[0], null);
        var undone = _store.Load(_store.List()[0]);

        Assert.Empty(new DriftChecker(_f.ToLive()).Check(undone));
        Assert.Equal(["sw.reg"], new DriftChecker(_f.ToLive()).Check(journal));
    }
}
