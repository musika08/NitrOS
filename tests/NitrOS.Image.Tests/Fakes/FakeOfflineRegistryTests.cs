namespace NitrOS.Image.Tests.Fakes;

using NitrOS.Core.Model;
using NitrOS.Image.Systems;
using NitrOS.Live.Systems;

public sealed class FakeOfflineRegistryTests
{
    [Fact]
    public void FakeHive_RefusedKeyReportsRefused()
    {
        var reg = new FakeOfflineRegistry();
        reg.RefuseWrite.Add("SOFTWARE\\Policies");

        using var hive = reg.Load("C:\\SOFTWARE", "NitrOS.Image.1.SOFTWARE");
        Assert.Equal(WriteCheck.Refused, hive.CanWrite("SOFTWARE\\Policies"));
        Assert.Equal(WriteCheck.Refused, hive.CanWrite("SOFTWARE\\Policies\\Sub"));
        Assert.Equal(WriteCheck.Writable, hive.CanWrite("SOFTWARE\\Allowed"));
    }

    [Fact]
    public void FakeHive_DeleteKeyRemovesTree()
    {
        var reg = new FakeOfflineRegistry();
        using (var hive = reg.Load("C:\\SOFTWARE", "NitrOS.Image.1.SOFTWARE"))
        {
            hive.SetValue("SOFTWARE\\Tree\\A", "valA", new RegValue(RegistryKind.Dword, "1"));
            hive.SetValue("SOFTWARE\\Tree\\B\\C", "valC", new RegValue(RegistryKind.String, "test"));
            hive.SetValue("SOFTWARE\\Other", "valO", new RegValue(RegistryKind.Dword, "2"));

            Assert.True(hive.KeyExists("SOFTWARE\\Tree\\B\\C"));

            var deleted = hive.DeleteKey("SOFTWARE\\Tree");
            Assert.True(deleted);

            Assert.False(hive.KeyExists("SOFTWARE\\Tree"));
            Assert.False(hive.KeyExists("SOFTWARE\\Tree\\A"));
            Assert.False(hive.KeyExists("SOFTWARE\\Tree\\B\\C"));
            Assert.True(hive.KeyExists("SOFTWARE\\Other"));
        }
    }

    [Fact]
    public void FakeRegistry_LoadedHivesSurviveSimulatedCrash()
    {
        var reg = new FakeOfflineRegistry();
        var h1 = reg.Load("C:\\SOFTWARE", "NitrOS.Image.1.SOFTWARE");
        var h2 = reg.Load("C:\\SYSTEM", "NitrOS.Image.1.SYSTEM");

        Assert.Equal(2, reg.LoadedNitrOSHives().Count);

        // Crash simulation leaves loaded hives in place (does not call Dispose/Unload)
        Assert.Throws<SimulatedCrashException>(() => reg.CrashOn("AfterLoad"));

        var leftovers = reg.LoadedNitrOSHives();
        Assert.Contains("NitrOS.Image.1.SOFTWARE", leftovers);
        Assert.Contains("NitrOS.Image.1.SYSTEM", leftovers);

        // Cleanup
        reg.Unload("NitrOS.Image.1.SOFTWARE");
        reg.Unload("NitrOS.Image.1.SYSTEM");
        Assert.Empty(reg.LoadedNitrOSHives());
    }
}
