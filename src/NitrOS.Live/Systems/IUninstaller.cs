namespace NitrOS.Live.Systems;

public sealed record UninstallEntry(string KeyPath, string DisplayName, string Publisher, string? Version, string? QuietUninstallString, string? UninstallString);

public interface IUninstaller
{
    IReadOnlyList<UninstallEntry> Find(string displayNamePattern, string publisher);
    bool SignerMatches(UninstallEntry entry, string publisher);
    /// <summary>Runs the uninstaller and returns its exit code.</summary>
    int Run(UninstallEntry entry, string? args);
}
