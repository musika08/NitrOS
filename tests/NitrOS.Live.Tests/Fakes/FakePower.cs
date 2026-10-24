using NitrOS.Live.Systems;

namespace NitrOS.Live.Tests.Fakes;

public sealed class FakePower : IPowerConfig
{
    public string Active { get; set; } = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public List<string> SchemeList { get; } = ["381b4222-f694-41f0-9685-ff5bb260df2e"];
    public Dictionary<(string Scheme, string Subgroup, string Setting), (int Ac, int Dc)> Settings { get; } = new(new Cmp());
    /// <summary>Matches a scheme GUID or a setting GUID.</summary>
    public string? FailOn { get; set; }

    private void Check(params string[] names)
    {
        if (FailOn is not null && names.Any(n => string.Equals(FailOn, n, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"FakePower failure on {FailOn}");
    }

    public string ActiveScheme() => Active;

    public IReadOnlyList<string> Schemes() => SchemeList;

    public (int Ac, int Dc)? Get(string scheme, string subgroup, string setting)
    {
        Check(scheme, setting);
        return Settings.TryGetValue((scheme, subgroup, setting), out var v) ? v : null;
    }

    public void SetAc(string scheme, string subgroup, string setting, int value)
    {
        Check(scheme, setting);
        var k = (scheme, subgroup, setting);
        Settings[k] = (value, Settings.TryGetValue(k, out var o) ? o.Dc : 0);
    }

    public void SetDc(string scheme, string subgroup, string setting, int value)
    {
        Check(scheme, setting);
        var k = (scheme, subgroup, setting);
        Settings[k] = (Settings.TryGetValue(k, out var o) ? o.Ac : 0, value);
    }

    public void SetActive(string scheme)
    {
        Check(scheme);
        if (!SchemeList.Contains(scheme, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown scheme {scheme}");
        Active = scheme;
    }

    private sealed class Cmp : IEqualityComparer<(string Scheme, string Subgroup, string Setting)>
    {
        public bool Equals((string Scheme, string Subgroup, string Setting) x, (string Scheme, string Subgroup, string Setting) y) =>
            string.Equals(x.Scheme, y.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Subgroup, y.Subgroup, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Setting, y.Setting, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Scheme, string Subgroup, string Setting) o) =>
            HashCode.Combine(o.Scheme.ToLowerInvariant(), o.Subgroup.ToLowerInvariant(), o.Setting.ToLowerInvariant());
    }
}
