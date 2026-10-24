using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakeUninstaller : IUninstaller
{
    public List<UninstallEntry> Entries { get; } = [];
    /// <summary>KeyPaths whose signer does not match.</summary>
    public HashSet<string> BadSigners { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int ExitCode { get; set; }
    /// <summary>Each Run call as (entry, args).</summary>
    public List<(UninstallEntry Entry, string? Args)> Runs { get; } = [];
    /// <summary>Matches a KeyPath or a DisplayName.</summary>
    public string? FailOn { get; set; }
    /// <summary>Called on every Run after it is recorded, for example to remove other entries the way a suite uninstaller does.</summary>
    public Action<UninstallEntry>? OnRun { get; set; }

    private void Check(UninstallEntry e)
    {
        if (FailOn is not null && (string.Equals(FailOn, e.KeyPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(FailOn, e.DisplayName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"FakeUninstaller failure on {FailOn}");
    }

    public IReadOnlyList<UninstallEntry> Find(string displayNamePattern, string publisher) =>
        Entries.Where(e => Glob.IsMatch(displayNamePattern, e.DisplayName)
            && string.Equals(e.Publisher, publisher, StringComparison.OrdinalIgnoreCase)).ToList();

    public bool SignerMatches(UninstallEntry entry, string publisher)
    {
        Check(entry);
        if (!Entries.Contains(entry)) return false; // the entry's key is gone, so its uninstaller cannot be checked
        return !BadSigners.Contains(entry.KeyPath);
    }

    public int Run(UninstallEntry entry, string? args)
    {
        Check(entry);
        Runs.Add((entry, args));
        OnRun?.Invoke(entry);
        return ExitCode;
    }
}
