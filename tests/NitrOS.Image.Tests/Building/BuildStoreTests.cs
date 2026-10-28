namespace NitrOS.Image.Tests.Building;

using System;
using System.IO;
using NitrOS.Core.Model;
using NitrOS.Image.Building;

public sealed class BuildStoreTests
{
    [Fact]
    public void Store_RoundTripsEveryActionTypeAndNullOutcome()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"nitros-build-store-{Guid.NewGuid():N}");
        try
        {
            var store = new BuildStore(temp);
            var req = new BuildRequest("C:\\source.iso", "C:\\out.iso", 1, ["sw1", "sw2"], CardId.Recommended);
            var fp = new SourceFingerprint(1234567, DateTimeOffset.UtcNow);

            var entries = new ImageEntry[]
            {
                new("sw1", 0, new AppxRemove("*Xbox*"), ImagePhase.Dism, ImageOutcome.Applied, "removed", DateTimeOffset.UtcNow),
                new("sw1", 1, new CapabilityRemove("Cap1"), ImagePhase.Dism, ImageOutcome.Deferred, "deferred", DateTimeOffset.UtcNow),
                new("sw1", 2, new FeatureDisable("Feat1"), ImagePhase.Dism, ImageOutcome.Skipped, "skipped", DateTimeOffset.UtcNow),
                new("sw1", 3, new PackageRemove("*Pkg*"), ImagePhase.Dism, ImageOutcome.Failed, "error", DateTimeOffset.UtcNow),
                new("sw2", 0, new FileRemove("%SystemRoot%\\file.txt"), ImagePhase.Files, ImageOutcome.Applied, null, DateTimeOffset.UtcNow),
                new("sw2", 1, new RegistrySet("HKLM", "SOFTWARE\\Test", "Val", RegistryKind.Dword, "1"), ImagePhase.Registry, ImageOutcome.Applied, null, DateTimeOffset.UtcNow),
                new("sw2", 2, new RegistryDelete("HKLM", "SOFTWARE\\Test", "Val"), ImagePhase.Registry, ImageOutcome.Applied, null, DateTimeOffset.UtcNow),
                new("sw2", 3, new ServiceStart("Svc1", ServiceStartType.Disabled), ImagePhase.Registry, ImageOutcome.Applied, null, DateTimeOffset.UtcNow),
                new("sw2", 4, new StartupDisable(StartupSource.Run, "RunVal"), ImagePhase.Registry, ImageOutcome.Applied, null, DateTimeOffset.UtcNow),
                new("sw2", 5, new TaskDisable("\\Path\\Task"), ImagePhase.FirstBoot, ImageOutcome.Deferred, null, DateTimeOffset.UtcNow),
                new("sw2", 6, new PowerSet("Sub", "Setting", 1, 0), ImagePhase.FirstBoot, ImageOutcome.Deferred, null, DateTimeOffset.UtcNow),
                new("sw2", 7, new Script("test"), ImagePhase.FirstBoot, ImageOutcome.Deferred, null, DateTimeOffset.UtcNow),
                new("sw2", 8, new FileRemove("%SystemRoot%\\in-flight.txt"), ImagePhase.Files, null, null, DateTimeOffset.UtcNow)
            };

            var state = new BuildState(
                "20261028-120000abcd",
                req,
                "dummyhash",
                fp,
                1,
                "Windows 11 Pro",
                BuildStep.ApplyDism,
                entries,
                temp,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow);

            store.Save(state);

            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(state.BuildId, loaded.BuildId);
            Assert.Equal(state.Request.OutputIso, loaded.Request.OutputIso);
            Assert.Equal(state.Next, loaded.Next);
            Assert.Equal(entries.Length, loaded.Entries.Count);
            Assert.Null(loaded.Entries[^1].Outcome);
            Assert.Equal("in-flight.txt", ((FileRemove)loaded.Entries[^1].Action).Path[^13..]);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void Store_SaveIsAtomicAndIgnoresTmp()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"nitros-build-store-atomic-{Guid.NewGuid():N}");
        try
        {
            var store = new BuildStore(temp);
            Directory.CreateDirectory(temp);
            File.WriteAllText(Path.Combine(temp, "build.json.tmp"), "{ corrupt json ");

            Assert.Null(store.TryLoad());

            var state = new BuildState(
                "20261028-120000abcd",
                new BuildRequest("src", "out", null, [], null),
                "hash",
                new SourceFingerprint(10, DateTimeOffset.UtcNow),
                1,
                null,
                BuildStep.Prepare,
                [],
                temp,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            store.Save(state);
            Assert.NotNull(store.TryLoad());
            Assert.False(File.Exists(Path.Combine(temp, "build.json.tmp")));
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void Store_TryLoadMissingIsNull()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"nitros-build-store-missing-{Guid.NewGuid():N}");
        try
        {
            var store = new BuildStore(temp);
            Assert.Null(store.TryLoad());
            Assert.Throws<FileNotFoundException>(() => store.Load());
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    [Fact]
    public void SelectionHash_IgnoresMeasured()
    {
        var sw1 = new SwitchDef
        {
            Id = "sw1",
            Name = "Switch 1",
            Feature = "Feature",
            Description = "Desc",
            Risk = Risk.Safe,
            Modes = [Mode.Image],
            Reversible = true,
            Cards = new CardDefaults
            {
                Recommended = CardDefault.Off,
                Gamer = CardDefault.Off,
                Streamer = CardDefault.Off,
                MaxLean = CardDefault.Off
            },
            Image = [new FileRemove("%SystemRoot%\\file.txt")],
            Measured = new Measurement(10, 1, "26100", new DateOnly(2026, 10, 28))
        };

        var sw1NoMeasure = sw1 with { Measured = null };

        var hash1 = SelectionHash.Of([sw1]);
        var hash2 = SelectionHash.Of([sw1NoMeasure]);
        Assert.Equal(hash1, hash2);

        var swModified = sw1 with { Image = [new FileRemove("%SystemRoot%\\file2.txt")] };
        var hash3 = SelectionHash.Of([swModified]);
        Assert.NotEqual(hash1, hash3);
    }
}
