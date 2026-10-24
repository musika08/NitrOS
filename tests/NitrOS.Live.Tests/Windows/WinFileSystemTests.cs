using NitrOS.Live.Windows;

namespace NitrOS.Live.Tests.Windows;

/// <summary>Every file touched here lives in the test's own temp folder.</summary>
public sealed class WinFileSystemTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "NitrOS-test-" + Guid.NewGuid().ToString("N"));

    public WinFileSystemTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (!Directory.Exists(_dir)) return;
        foreach (var f in Directory.EnumerateFileSystemEntries(_dir, "*", SearchOption.AllDirectories))
            try { File.SetAttributes(f, FileAttributes.Normal); } catch (IOException) { }
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Exists_FilesAndFolders()
    {
        var fs = new WinFileSystem();
        var f = Path.Combine(_dir, "a.txt");
        File.WriteAllText(f, "x");
        Assert.True(fs.Exists(f));
        Assert.True(fs.Exists(_dir));
        Assert.False(fs.Exists(Path.Combine(_dir, "missing.txt")));
    }

    [Fact]
    public void TakeOwnershipAndDelete_DeletesReadOnlyFileInTempFolder()
    {
        var f = Path.Combine(_dir, "readonly.txt");
        File.WriteAllText(f, "x");
        File.SetAttributes(f, FileAttributes.ReadOnly);
        var keep = Path.Combine(_dir, "keep.txt");
        File.WriteAllText(keep, "keep");

        new WinFileSystem().TakeOwnershipAndDelete(f);

        Assert.False(File.Exists(f));
        Assert.Equal("keep", File.ReadAllText(keep));
    }

    [Fact]
    public void TakeOwnershipAndDelete_DeletesFolderTreeInTempFolder()
    {
        var target = Path.Combine(_dir, "tree");
        Directory.CreateDirectory(Path.Combine(target, "sub"));
        File.WriteAllText(Path.Combine(target, "sub", "ro.txt"), "x");
        File.SetAttributes(Path.Combine(target, "sub", "ro.txt"), FileAttributes.ReadOnly | FileAttributes.Hidden);
        var sibling = Path.Combine(_dir, "sibling");
        Directory.CreateDirectory(sibling);

        new WinFileSystem().TakeOwnershipAndDelete(target);

        Assert.False(Directory.Exists(target));
        Assert.True(Directory.Exists(sibling));
    }

    [Fact]
    public void TakeOwnershipAndDelete_RefusesTreeWithJunction()
    {
        var target = Path.Combine(_dir, "tree");
        var outside = Path.Combine(_dir, "outside");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "precious.txt"), "keep");
        var link = Path.Combine(target, "link");
        var r = ProcessRunner.Run("cmd.exe", ["/c", "mklink", "/J", link, outside], TimeSpan.FromSeconds(20));
        Assert.Equal(0, r.ExitCode);

        try
        {
            Assert.Throws<InvalidOperationException>(() => new WinFileSystem().TakeOwnershipAndDelete(target));
            Assert.True(Directory.Exists(target));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(outside, "precious.txt")));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void ListTree_TargetFirstThenParentsBeforeChildren()
    {
        var target = Path.Combine(_dir, "tree");
        Directory.CreateDirectory(Path.Combine(target, "a", "b"));
        File.WriteAllText(Path.Combine(target, "a", "b", "f.txt"), "x");
        File.WriteAllText(Path.Combine(target, "top.txt"), "x");

        var list = WinFileSystem.ListTree(target, isDir: true);

        Assert.Equal(target, list[0]);
        Assert.Equal(5, list.Count);
        Assert.True(list.IndexOf(Path.Combine(target, "a")) < list.IndexOf(Path.Combine(target, "a", "b")));
        Assert.True(list.IndexOf(Path.Combine(target, "a", "b")) < list.IndexOf(Path.Combine(target, "a", "b", "f.txt")));
        Assert.Equal(new[] { Path.Combine(_dir, "x.txt") }, WinFileSystem.ListTree(WriteFile("x.txt"), isDir: false));
    }

    private string WriteFile(string name)
    {
        var p = Path.Combine(_dir, name);
        File.WriteAllText(p, "x");
        return p;
    }

    [Fact]
    public void TakeOwnershipAndDelete_MissingIsANoOp()
    {
        new WinFileSystem().TakeOwnershipAndDelete(Path.Combine(_dir, "missing.txt"));
    }

    [Fact]
    public void ValidateTarget_RefusesDangerousPaths()
    {
        foreach (var bad in new[]
        {
            "", "relative\\file.txt", _dir + "\\*.txt", "C:\\", "C:", Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory + "\\", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @"\\server\share\x", @"\\?\C:\Windows\x",
        })
            Assert.Throws<ArgumentException>(() => WinFileSystem.ValidateTarget(bad));
        Assert.Equal(Path.Combine(_dir, "x.txt"), WinFileSystem.ValidateTarget(Path.Combine(_dir, "x.txt")));
    }
}
