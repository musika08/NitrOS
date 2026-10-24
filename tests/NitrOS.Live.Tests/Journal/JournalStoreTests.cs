using NitrOS.Core.Model;
using NitrOS.Live.Journal;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Journal;

public class JournalStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nitros-journal-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static RunJournal Journal(string id, params JournalEntry[] entries) =>
        new(id, "S-1-5-21-1", "26100", ["sw.one"], entries);

    private static readonly SwitchAction[] Actions =
    [
        new TaskDisable("\\Microsoft\\X"),
        new PowerSet("sub", "set", null, null),
        new PowerSet("sub", "set", 5, 0),
        new StartupDisable(StartupSource.Run, "Foo", "HKCU"),
        new Script("echo hi", "echo undo"),
        new Script("echo hi"),
        new LaunchAtLogon("%ProgramFiles%\\X\\x.exe", "--tray"),
        new RegistryDelete("HKLM", "SOFTWARE\\X"),
    ];

    private static JournalEntry Entry(PriorState prior, int i = 0) =>
        new("sw.one", i, Actions[i % Actions.Length], prior, ActionOutcome.Applied, null,
            new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void JournalStore_RoundTripsEveryPriorKind()
    {
        var val = new RegValue(RegistryKind.MultiString, "a\nb");
        var tree = new RegTree("SOFTWARE\\X", [new RegNamedValue("v", val)],
            [new RegTree("SOFTWARE\\X\\Sub", [], [])]);
        PriorState[] priors =
        [
            new RegistryPrior(val),
            new RegistryPrior(null),
            new RegistryTreePrior(tree, null),
            new RegistryTreePrior(null, val),
            new ServicePrior([new ServiceStartEntry("CDPUserSvc", ServiceStartType.Automatic), new ServiceStartEntry("CDPUserSvc_1a2b", ServiceStartType.Manual)]),
            new TaskPrior(true),
            new StartupPrior([new StartupEntry("HKCU\\Run\\Foo", "02 00 00 00 00 00 00 00 00 00 00 00")]),
            new PowerPrior("381b4222", 50, null),
            new SchemePrior("8c5e7fda"),
            new LaunchPrior(null),
            new MissingPrior(),
            new IrreversiblePrior("Microsoft.BingNews", "1.2.3", "Reinstall from the Microsoft Store"),
            new FeaturePrior("Enabled"),
        ];
        var journal = Journal("run-1", priors.Select((p, i) => Entry(p, i)).ToArray());

        var store = new JournalStore(_dir);
        var path = store.Save(journal);
        var loaded = store.Load(path);

        Assert.Equal(journal.RunId, loaded.RunId);
        Assert.Equal(journal.UserSid, loaded.UserSid);
        Assert.Equal(journal.WindowsBuild, loaded.WindowsBuild);
        Assert.Equal(journal.SwitchIds, loaded.SwitchIds);
        Assert.Equal(priors.Length, loaded.Entries.Count);
        for (var i = 0; i < priors.Length; i++)
        {
            var a = journal.Entries[i];
            var b = loaded.Entries[i];
            Assert.Equal(priors[i].GetType(), b.Prior.GetType());
            Assert.Equal(a.Action, b.Action);
            Assert.Equal(a.Outcome, b.Outcome);
            Assert.Equal(a.At, b.At);
        }
        // Records holding lists compare lists by reference, so check the nested content directly.
        Assert.Equal(val, ((RegistryPrior)loaded.Entries[0].Prior).Value);
        Assert.Null(((RegistryPrior)loaded.Entries[1].Prior).Value);
        var lt = Assert.IsType<RegistryTreePrior>(loaded.Entries[2].Prior).Tree!;
        Assert.Equal("SOFTWARE\\X", lt.Key);
        Assert.Equal(val, Assert.Single(lt.Values).Value);
        Assert.Equal("SOFTWARE\\X\\Sub", Assert.Single(lt.SubKeys).Key);
        Assert.Equal(val, ((RegistryTreePrior)loaded.Entries[3].Prior).Value);
        var svc = (ServicePrior)loaded.Entries[4].Prior;
        Assert.Equal(["CDPUserSvc", "CDPUserSvc_1a2b"], svc.Services.Select(s => s.Name));
        Assert.Equal([ServiceStartType.Automatic, ServiceStartType.Manual], svc.Services.Select(s => s.Start));
        Assert.True(((TaskPrior)loaded.Entries[5].Prior).Enabled);
        Assert.Equal(new StartupEntry("HKCU\\Run\\Foo", "02 00 00 00 00 00 00 00 00 00 00 00"),
            Assert.Single(((StartupPrior)loaded.Entries[6].Prior).Entries));
        Assert.Equal(new PowerPrior("381b4222", 50, null), loaded.Entries[7].Prior);
        Assert.Equal(new SchemePrior("8c5e7fda"), loaded.Entries[8].Prior);
        Assert.Equal(new LaunchPrior(null), loaded.Entries[9].Prior);
        Assert.Equal(new MissingPrior(), loaded.Entries[10].Prior);
        Assert.Equal(new IrreversiblePrior("Microsoft.BingNews", "1.2.3", "Reinstall from the Microsoft Store"), loaded.Entries[11].Prior);
        Assert.Equal(new FeaturePrior("Enabled"), loaded.Entries[12].Prior);
        Assert.Contains("\"kind\": \"registry-tree\"", File.ReadAllText(path));
    }

    [Fact]
    public void JournalStore_SaveIsAtomic()
    {
        var store = new JournalStore(_dir);
        Directory.CreateDirectory(_dir);
        var tmp = Path.Combine(_dir, "run-2.json.tmp");
        File.WriteAllText(tmp, "{ half written");

        Assert.Empty(store.List());

        var path = store.Save(Journal("run-2", Entry(new MissingPrior())));

        Assert.False(File.Exists(tmp));
        Assert.Equal([path], store.List());
        Assert.Single(store.Load(path).Entries);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void JournalStore_ListNewestFirst()
    {
        var store = new JournalStore(_dir);
        var a = store.Save(Journal("20261001-090000"));
        var b = store.Save(Journal("20261003-090000"));
        var c = store.Save(Journal("20261002-090000"));

        Assert.Equal([b, c, a], store.List());

        // Re-saving the OLDEST run (a later write time) must not move it to the front.
        File.SetLastWriteTimeUtc(a, DateTime.UtcNow.AddDays(1));
        store.Save(Journal("20261001-090000", Entry(new MissingPrior())));
        Assert.Equal([b, c, a], store.List());
    }

    [Fact]
    public void JournalStore_ListPutsSuffixedRunNewer()
    {
        var store = new JournalStore(_dir);
        var first = store.Save(Journal("20261025-120000"));
        var second = store.Save(Journal("20261025-120000-2"));
        var third = store.Save(Journal("20261025-120000-3"));
        var later = store.Save(Journal("20261025-120001"));

        Assert.Equal([later, third, second, first], store.List());
    }

    [Fact]
    public void EnsureHook_RunsOnceBeforeFirstUse()
    {
        var calls = 0;
        var store = new JournalStore(_dir, () => calls++);
        Assert.Equal(0, calls);
        Assert.Empty(store.List());
        var path = store.Save(Journal("20261025-120000"));
        store.Load(path);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void EnsureHook_FailureStopsAccessAndIsRetried()
    {
        var fail = true;
        var calls = 0;
        var store = new JournalStore(_dir, () => { calls++; if (fail) throw new UnauthorizedAccessException("not elevated"); });
        Assert.Throws<UnauthorizedAccessException>(() => store.List());
        Assert.Throws<UnauthorizedAccessException>(() => store.Save(Journal("20261025-120000")));
        Assert.False(Directory.Exists(_dir));
        fail = false;
        Assert.Empty(store.List());
        Assert.Equal(3, calls);
    }
}
