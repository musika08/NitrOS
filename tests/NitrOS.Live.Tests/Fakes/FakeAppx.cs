using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

/// <summary>
/// Packages are exact names; a name passed in may carry a star and is matched with Glob. ProvisionedThrows and
/// DeprovisionThrows make IsProvisioned / Deprovision throw that exception (for example an UnauthorizedAccessException).
/// </summary>
public sealed class FakeAppx : IAppx
{
    public HashSet<string> Installed { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Provisioned { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? FailOn { get; set; }
    public Exception? ProvisionedThrows { get; set; }
    public Exception? DeprovisionThrows { get; set; }
    public List<string> Removed { get; } = [];
    public List<string> Deprovisioned { get; } = [];

    private void Check(string name)
    {
        if (FailOn is not null && string.Equals(FailOn, name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"FakeAppx failure on {name}");
    }

    public bool IsInstalled(string name)
    {
        Check(name);
        return Installed.Any(p => Glob.IsMatch(name, p));
    }

    public bool IsProvisioned(string name)
    {
        Check(name);
        if (ProvisionedThrows is not null) throw ProvisionedThrows;
        return Provisioned.Any(p => Glob.IsMatch(name, p));
    }

    public void RemoveForAllUsers(string name)
    {
        Check(name);
        Removed.Add(name);
        Installed.RemoveWhere(p => Glob.IsMatch(name, p));
    }

    public void Deprovision(string name)
    {
        Check(name);
        if (DeprovisionThrows is not null) throw DeprovisionThrows;
        Deprovisioned.Add(name);
        Provisioned.RemoveWhere(p => Glob.IsMatch(name, p));
    }
}
