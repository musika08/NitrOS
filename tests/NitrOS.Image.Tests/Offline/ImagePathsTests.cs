namespace NitrOS.Image.Tests.Offline;

using System;
using System.IO;
using NitrOS.Core.Loading;
using NitrOS.Core.Model;
using NitrOS.Image.Offline;

public sealed class ImagePathsTests
{
    [Fact]
    public void ImagePaths_ExpandsEveryCatalogVariable()
    {
        var cur = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (cur is not null && !Directory.Exists(Path.Combine(cur.FullName, "catalog")))
        {
            cur = cur.Parent;
        }
        Assert.NotNull(cur);
        var catalogDir = Path.Combine(cur.FullName, "catalog");
        var loadResult = CatalogLoader.Load(catalogDir);
        Assert.NotNull(loadResult.Catalog);

        const string mountRoot = "W:\\m";

        foreach (var sw in loadResult.Catalog.Switches)
        {
            foreach (var action in sw.Image)
            {
                if (action is FileRemove fr)
                {
                    var expanded = ImagePaths.Expand(fr.Path, mountRoot);
                    Assert.StartsWith(mountRoot, expanded, StringComparison.OrdinalIgnoreCase);
                    Assert.True(ImagePaths.IsUnder(expanded, mountRoot));
                }
            }
        }
    }

    [Fact]
    public void ImagePaths_UnknownVariableThrows()
    {
        Assert.Throws<ArgumentException>(() => ImagePaths.Expand("%UnknownVar%\\test.txt", "W:\\m"));
    }

    [Fact]
    public void ImagePaths_NeverReadsHostEnvironment()
    {
        Environment.SetEnvironmentVariable("NITROS_TEST_X", "HostVal");
        try
        {
            Assert.Throws<ArgumentException>(() => ImagePaths.Expand("%NITROS_TEST_X%\\test.txt", "W:\\m"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NITROS_TEST_X", null);
        }
    }

    [Fact]
    public void ImagePaths_RefusesEscapes()
    {
        const string mountRoot = "W:\\mount";

        // Path escaping with ..
        Assert.False(ImagePaths.IsUnder("W:\\mount\\Windows\\..\\..\\escape.txt", mountRoot));

        // Sibling folder with same prefix
        Assert.False(ImagePaths.IsUnder("W:\\mount2\\file.txt", mountRoot));

        // Different drive
        Assert.False(ImagePaths.IsUnder("C:\\mount\\file.txt", mountRoot));

        // Valid subpath
        Assert.True(ImagePaths.IsUnder("W:\\mount\\Windows\\System32\\file.txt", mountRoot));
    }
}
