using System.Globalization;
using System.Text.RegularExpressions;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>powercfg adapter. Scheme GUIDs are always returned lowercase without braces; any case is accepted.</summary>
public sealed class WinPowerConfig : IPowerConfig
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly Regex GuidRx = new(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
    private static readonly Regex HexRx = new(@":\s*0x([0-9a-fA-F]+)\s*$", RegexOptions.Compiled);

    /// <summary>Validates that the text is a GUID (braces allowed, any case) and returns it lowercase without braces.</summary>
    public static string Normalise(string guid)
    {
        if (!Guid.TryParse(guid?.Trim(), out var g)) throw new ArgumentException($"'{guid}' is not a GUID", nameof(guid));
        return g.ToString("D");
    }

    /// <summary>powercfg exits 1 when the scheme, subgroup or setting does not exist. Any other non-zero exit is a real failure.</summary>
    public static bool IsNotFoundExit(int exitCode) => exitCode == 1;

    public string ActiveScheme()
    {
        var (code, stdout, err) = ProcessRunner.Run("powercfg.exe", new[] { "/getactivescheme" }, Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg /getactivescheme failed (exit {code}): {err.Trim()}");
        var m = GuidRx.Match(stdout);
        if (!m.Success) throw new InvalidOperationException("Could not read the active power scheme.");
        return Normalise(m.Value);
    }

    public IReadOnlyList<string> Schemes()
    {
        var (code, stdout, err) = ProcessRunner.Run("powercfg.exe", new[] { "/list" }, Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg /list failed (exit {code}): {err.Trim()}");
        return GuidRx.Matches(stdout).Select(m => Normalise(m.Value)).Distinct().ToList();
    }

    public (int Ac, int Dc)? Get(string scheme, string subgroup, string setting)
    {
        var args = new[] { Normalise(scheme), Normalise(subgroup), Normalise(setting) };
        // /q lists visible settings; /qh also lists hidden ones, so fall back to it when /q does not show the setting.
        foreach (var flag in new[] { "/q", "/qh" })
        {
            var (code, stdout, err) = ProcessRunner.Run("powercfg.exe", new[] { flag }.Concat(args).ToArray(), Timeout);
            if (code == 0)
            {
                if (ParseIndexes(stdout) is { } v) return v;
            }
            else if (!IsNotFoundExit(code))
                throw new InvalidOperationException($"powercfg {flag} failed (exit {code}): {err.Trim()}");
        }
        return null;
    }

    /// <summary>The AC and DC index lines are the last two hex lines; earlier ones are min/max/increment. Language independent.</summary>
    public static (int Ac, int Dc)? ParseIndexes(string output)
    {
        var values = new List<long>();
        foreach (var line in output.Split('\n'))
        {
            var m = HexRx.Match(line.TrimEnd('\r'));
            if (m.Success) values.Add(long.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        if (values.Count < 2) return null;
        return (unchecked((int)values[^2]), unchecked((int)values[^1]));
    }

    public void SetAc(string scheme, string subgroup, string setting, int value) =>
        Run("/setacvalueindex", Normalise(scheme), Normalise(subgroup), Normalise(setting), value.ToString(CultureInfo.InvariantCulture));

    public void SetDc(string scheme, string subgroup, string setting, int value) =>
        Run("/setdcvalueindex", Normalise(scheme), Normalise(subgroup), Normalise(setting), value.ToString(CultureInfo.InvariantCulture));

    public void SetActive(string scheme) => Run("/setactive", Normalise(scheme));

    private static void Run(params string[] args)
    {
        var (code, _, err) = ProcessRunner.Run("powercfg.exe", args, Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg {args[0]} failed (exit {code}): {err.Trim()}");
    }
}
