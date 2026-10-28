using NitrOS.Core.Loading;
using NitrOS.Core.Model;

namespace NitrOS.App.Tests;

public static class TestCatalog
{
    public static Catalog LoadRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("global.json not found above " + AppContext.BaseDirectory);
        var catalogDir = Path.Combine(dir.FullName, "catalog");
        var result = CatalogLoader.Load(catalogDir);
        if (!result.Ok || result.Catalog is null)
            throw new InvalidOperationException("Failed to load catalog: " + string.Join(", ", result.Errors.Select(e => e.Message)));
        return result.Catalog;
    }
}
