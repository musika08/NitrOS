using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

/// <summary>Packages are exact names; a name passed in may carry a star and is matched with Glob.</summary>
public sealed class FakeAppx : IAppx
{
    public HashSet<string> Installed { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Provisioned { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? FailOn { get; set; }

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
        return Provisioned.Any(p => Glob.IsMatch(name, p));
    }

    public void RemoveForAllUsers(string name)
    {
        Check(name);
        Installed.RemoveWhere(p => Glob.IsMatch(name, p));
    }

    public void Deprovision(string name)
    {
        Check(name);
        Provisioned.RemoveWhere(p => Glob.IsMatch(name, p));
    }
}
