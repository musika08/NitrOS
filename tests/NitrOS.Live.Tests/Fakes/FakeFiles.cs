using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeFiles : IFileSystem
{
    public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Deleted { get; } = [];
    public string? FailOn { get; set; }

    private void Check(string path)
    {
        if (FailOn is not null && string.Equals(FailOn, path, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"FakeFiles failure on {path}");
    }

    public bool Exists(string path)
    {
        Check(path);
        return Existing.Contains(path);
    }

    public void TakeOwnershipAndDelete(string path)
    {
        Check(path);
        Existing.Remove(path);
        Deleted.Add(path);
    }
}
