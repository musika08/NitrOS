namespace NitrOS.Core.Tests.TestSupport;

public static class RepoPaths
{
    public static string CatalogDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("global.json not found above " + AppContext.BaseDirectory);
        return Path.Combine(dir.FullName, "catalog");
    }
}
