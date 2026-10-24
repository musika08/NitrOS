using Microsoft.Win32;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Writes only under HKCU\Software\NitrOS.Tests\&lt;guid&gt;, which Dispose deletes in both views.</summary>
public sealed class WinRegistryTests : IDisposable
{
    private const string Parent = @"Software\NitrOS.Tests";
    private readonly string _root = Parent + "\\" + Guid.NewGuid().ToString("N");
    private readonly WinRegistry _reg = new();

    public void Dispose()
    {
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            using var b = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
            b.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);
            using var p = b.OpenSubKey(Parent);
            if (p is not null && p.SubKeyCount == 0 && p.ValueCount == 0)
                b.DeleteSubKey(Parent, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void WinRegistry_RoundTripsEveryKindUnderSandboxKey()
    {
        var h = LiveHive.CurrentUser;
        var v = RegView.Default;
        var cases = new (string Name, RegValue Value)[]
        {
            ("d", new RegValue(RegistryKind.Dword, "4294967295")),
            ("d0", new RegValue(RegistryKind.Dword, "0")),
            ("q", new RegValue(RegistryKind.Qword, "18446744073709551615")),
            ("s", new RegValue(RegistryKind.String, "hello world")),
            ("e", new RegValue(RegistryKind.ExpandString, "%SystemRoot%\\x")),
            ("m", new RegValue(RegistryKind.MultiString, "one\ntwo\nthree")),
            ("b", new RegValue(RegistryKind.Binary, "00FF10AB")),
        };
        foreach (var (name, value) in cases) _reg.SetValue(h, _root, name, value, v);

        foreach (var (name, value) in cases)
            Assert.Equal(value, _reg.GetValue(h, _root, name, v));

        // Expand strings are neither expanded on write nor on read, and keep their kind.
        using (var b = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64))
        using (var k = b.OpenSubKey(_root)!)
            Assert.Equal(RegistryValueKind.ExpandString, k.GetValueKind("e"));

        // Binary hex written in lowercase comes back uppercase.
        _reg.SetValue(h, _root, "b", new RegValue(RegistryKind.Binary, "ab01cd"), v);
        Assert.Equal("AB01CD", _reg.GetValue(h, _root, "b", v)!.Data);

        Assert.Equal(cases.Select(c => c.Name).Order(), _reg.GetValueNames(h, _root, v).Order());
        Assert.Null(_reg.GetValue(h, _root, "missing", v));
        Assert.Null(_reg.GetValue(h, _root + "\\nokey", "d", v));

        _reg.DeleteValue(h, _root, "d", v);
        Assert.Null(_reg.GetValue(h, _root, "d", v));
        _reg.DeleteValue(h, _root, "d", v); // missing value: no throw
        _reg.DeleteValue(h, _root + "\\nokey", "d", v); // missing key: no throw
        Assert.True(_reg.KeyExists(h, _root, v));
        _reg.DeleteKey(h, _root, v);
        Assert.False(_reg.KeyExists(h, _root, v));
        _reg.DeleteKey(h, _root, v); // already gone: no throw
    }

    [Fact]
    public void WinRegistry_ExportImportTree()
    {
        var h = LiveHive.CurrentUser;
        var v = RegView.Default;
        _reg.SetValue(h, _root + "\\A", "x", new RegValue(RegistryKind.Dword, "7"), v);
        _reg.SetValue(h, _root + "\\A\\B", "y", new RegValue(RegistryKind.String, "deep"), v);
        _reg.SetValue(h, _root + "\\A", "", new RegValue(RegistryKind.String, "default"), v);

        var tree = _reg.ExportKey(h, _root + "\\A", v)!;
        Assert.Equal(_root + "\\A", tree.Key);
        Assert.Equal(2, tree.Values.Count);
        Assert.Single(tree.SubKeys);
        Assert.Equal(_root + "\\A\\B", tree.SubKeys[0].Key);
        Assert.Null(_reg.ExportKey(h, _root + "\\Nope", v));

        _reg.DeleteKey(h, _root + "\\A", v);
        Assert.False(_reg.KeyExists(h, _root + "\\A", v));

        _reg.ImportKey(h, tree, v);
        Assert.Equal(new RegValue(RegistryKind.Dword, "7"), _reg.GetValue(h, _root + "\\A", "x", v));
        Assert.Equal(new RegValue(RegistryKind.String, "default"), _reg.GetValue(h, _root + "\\A", "", v));
        Assert.Equal(new RegValue(RegistryKind.String, "deep"), _reg.GetValue(h, _root + "\\A\\B", "y", v));
    }

    [Fact]
    public void WinRegistry_Registry32View()
    {
        // HKCU\Software is shared between the 32 and 64-bit views, so this proves the Registry32 round trip only, not separation.
        var h = LiveHive.CurrentUser;
        _reg.SetValue(h, _root, "v32", new RegValue(RegistryKind.Dword, "32"), RegView.Registry32);
        Assert.Equal(new RegValue(RegistryKind.Dword, "32"), _reg.GetValue(h, _root, "v32", RegView.Registry32));
        Assert.True(_reg.KeyExists(h, _root, RegView.Registry32));
        _reg.DeleteKey(h, _root, RegView.Registry32);
        Assert.False(_reg.KeyExists(h, _root, RegView.Registry32));
    }

    [Fact]
    public void WinRegistry_ReadsRealWindowsReadOnly()
    {
        var v = _reg.GetValue(LiveHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName", RegView.Default);
        Assert.NotNull(v);
        Assert.Equal(RegistryKind.String, v!.Kind);
    }

    [Fact]
    public void WinRegistry_UnsupportedKindThrowsInsteadOfBeingDropped()
    {
        using (var b = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Registry64))
        using (var k = b.CreateSubKey(_root, writable: true))
            k.SetValue("none", new byte[] { 1, 2 }, RegistryValueKind.None);

        var ex = Assert.Throws<NotSupportedException>(() => _reg.GetValue(LiveHive.CurrentUser, _root, "none", RegView.Default));
        Assert.Contains("None", ex.Message);
        Assert.Contains(_root + "\\none", ex.Message);
        Assert.Throws<NotSupportedException>(() => _reg.ExportKey(LiveHive.CurrentUser, _root, RegView.Default));
    }

    [Fact]
    public void WinRegistry_WritesHexNumbersAndReadsDecimal()
    {
        var h = LiveHive.CurrentUser;
        _reg.SetValue(h, _root, "d", new RegValue(RegistryKind.Dword, "0x10"), RegView.Default);
        _reg.SetValue(h, _root, "big", new RegValue(RegistryKind.Dword, "0xFFFFFFFF"), RegView.Default);
        _reg.SetValue(h, _root, "q", new RegValue(RegistryKind.Qword, "0X10"), RegView.Default);
        Assert.Equal("16", _reg.GetValue(h, _root, "d", RegView.Default)!.Data);
        Assert.Equal("4294967295", _reg.GetValue(h, _root, "big", RegView.Default)!.Data);
        Assert.Equal("16", _reg.GetValue(h, _root, "q", RegView.Default)!.Data);
        Assert.Throws<FormatException>(() => _reg.SetValue(h, _root, "bad", new RegValue(RegistryKind.Dword, "0x100000000"), RegView.Default));
        Assert.Throws<FormatException>(() => _reg.SetValue(h, _root, "bad", new RegValue(RegistryKind.Dword, "12abc"), RegView.Default));
    }
}
