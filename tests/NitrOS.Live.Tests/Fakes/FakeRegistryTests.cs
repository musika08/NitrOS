using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public class FakeRegistryTests
{
    private static readonly RegValue One = new(RegistryKind.Dword, "1");

    [Fact]
    public void FakeRegistry_SetValueCreatesAncestors()
    {
        var r = new FakeRegistry();
        r.SetValue(LiveHive.CurrentUser, "A\\B\\C", "v", One, RegView.Default);

        Assert.True(r.KeyExists(LiveHive.CurrentUser, "A", RegView.Default));
        Assert.True(r.KeyExists(LiveHive.CurrentUser, "A\\B", RegView.Default));
        Assert.True(r.KeyExists(LiveHive.CurrentUser, "a\\b\\c", RegView.Default));
        Assert.False(r.KeyExists(LiveHive.LocalMachine, "A", RegView.Default));
        Assert.False(r.KeyExists(LiveHive.CurrentUser, "A\\X", RegView.Default));
    }

    [Fact]
    public void FakeRegistry_SeededValueMakesKeyExist()
    {
        var r = new FakeRegistry();
        r.Values[(LiveHive.LocalMachine, "S\\K\\Leaf", "n", RegView.Default)] = One;

        Assert.True(r.KeyExists(LiveHive.LocalMachine, "S\\K\\Leaf", RegView.Default));
        Assert.True(r.KeyExists(LiveHive.LocalMachine, "S\\K", RegView.Default));
        Assert.True(r.KeyExists(LiveHive.LocalMachine, "S", RegView.Default));
        var tree = r.ExportKey(LiveHive.LocalMachine, "S\\K", RegView.Default);
        Assert.NotNull(tree);
        Assert.Equal("S\\K\\Leaf", Assert.Single(tree.SubKeys).Key);
    }

    [Fact]
    public void FakeRegistry_ExportIncludesDeepSubtree()
    {
        var r = new FakeRegistry();
        r.SetValue(LiveHive.CurrentUser, "R", "top", One, RegView.Default);
        r.SetValue(LiveHive.CurrentUser, "R\\A\\B\\C", "deep", One, RegView.Default);
        r.SetValue(LiveHive.CurrentUser, "R\\A2", "side", One, RegView.Default);

        var tree = r.ExportKey(LiveHive.CurrentUser, "R", RegView.Default)!;

        Assert.Equal("top", Assert.Single(tree.Values).Name);
        Assert.Equal(2, tree.SubKeys.Count);
        var a = tree.SubKeys.Single(s => s.Key == "R\\A");
        var b = Assert.Single(a.SubKeys);
        Assert.Equal("R\\A\\B", b.Key);
        var c = Assert.Single(b.SubKeys);
        Assert.Equal("R\\A\\B\\C", c.Key);
        Assert.Equal("deep", Assert.Single(c.Values).Name);

        // Round trip: delete and import restores everything.
        r.DeleteKey(LiveHive.CurrentUser, "R", RegView.Default);
        Assert.False(r.KeyExists(LiveHive.CurrentUser, "R", RegView.Default));
        r.ImportKey(LiveHive.CurrentUser, tree, RegView.Default);
        Assert.Equal(One, r.GetValue(LiveHive.CurrentUser, "R\\A\\B\\C", "deep", RegView.Default));
        Assert.True(r.KeyExists(LiveHive.CurrentUser, "R\\A\\B", RegView.Default));
    }
}
