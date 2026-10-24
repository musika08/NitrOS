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

    public static string Normalise(string guid) => guid.Trim().Trim('{', '}').ToLowerInvariant();

    public string ActiveScheme()
    {
        var (code, stdout, err) = ProcessRunner.Run("powercfg.exe", "/getactivescheme", Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg /getactivescheme failed: {err.Trim()}");
        var m = GuidRx.Match(stdout);
        if (!m.Success) throw new InvalidOperationException("Could not read the active power scheme.");
        return Normalise(m.Value);
    }

    public IReadOnlyList<string> Schemes()
    {
        var (code, stdout, err) = ProcessRunner.Run("powercfg.exe", "/list", Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg /list failed: {err.Trim()}");
        return GuidRx.Matches(stdout).Select(m => Normalise(m.Value)).Distinct().ToList();
    }

    public (int Ac, int Dc)? Get(string scheme, string subgroup, string setting)
    {
        var (code, stdout, _) = ProcessRunner.Run("powercfg.exe", $"/q {Normalise(scheme)} {Normalise(subgroup)} {Normalise(setting)}", Timeout);
        if (code != 0) return null;
        return ParseIndexes(stdout);
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
        Run($"/setacvalueindex {Normalise(scheme)} {Normalise(subgroup)} {Normalise(setting)} {value}");

    public void SetDc(string scheme, string subgroup, string setting, int value) =>
        Run($"/setdcvalueindex {Normalise(scheme)} {Normalise(subgroup)} {Normalise(setting)} {value}");

    public void SetActive(string scheme) => Run($"/setactive {Normalise(scheme)}");

    private static void Run(string args)
    {
        var (code, _, err) = ProcessRunner.Run("powercfg.exe", args, Timeout);
        if (code != 0) throw new InvalidOperationException($"powercfg {args} failed (exit {code}): {err.Trim()}");
    }
}
