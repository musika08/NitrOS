using System.Text.RegularExpressions;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>Thrown when DISM refuses online servicing; the message is the user-facing detail.</summary>
public sealed class DismAccessDeniedException() : InvalidOperationException(WinDism.AccessDeniedDetail);

/// <summary>
/// dism.exe adapter (always /English so the output can be parsed). States are normalised to plain words without spaces
/// (Enabled, Disabled, EnablePending, DisablePending, NotPresent, Installed, ...). Access denied throws
/// <see cref="DismAccessDeniedException"/>, which the engine reports as Failed with <see cref="AccessDeniedDetail"/>.
/// </summary>
public sealed class WinDism : IDism
{
    public const string AccessDeniedDetail = "Windows refused this DISM change (access denied).";
    public const int RestartExitCode = 3010;
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ChangeTimeout = TimeSpan.FromMinutes(30);
    private static readonly Regex StateRx = new(@"^\s*State\s*:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex NameRx = new(@"^[A-Za-z0-9][A-Za-z0-9._~\-]*$", RegexOptions.Compiled);

    /// <summary>Every state DISM reports for features and capabilities, without spaces. Anything else throws.</summary>
    private static readonly string[] KnownStates =
    [
        "Enabled", "Disabled", "EnablePending", "DisablePending", "DisabledWithPayloadRemoved",
        "NotPresent", "Installed", "InstallPending", "UninstallPending", "Staged", "Superseded", "PartiallyInstalled",
        "Removed",
    ];

    private static string Dism => Path.Combine(Environment.SystemDirectory, "dism.exe");

    public string? FeatureState(string name) => Query("/Get-FeatureInfo", "/FeatureName:", name);

    public string? CapabilityState(string name) => Query("/Get-CapabilityInfo", "/CapabilityName:", name);

    public DismOutcome DisableFeature(string name) => Change(["/Disable-Feature", "/FeatureName:" + Checked(name), "/NoRestart"]);

    public DismOutcome EnableFeature(string name) => Change(["/Enable-Feature", "/FeatureName:" + Checked(name), "/NoRestart"]);

    public DismOutcome RemoveCapability(string name) => Change(["/Remove-Capability", "/CapabilityName:" + Checked(name), "/NoRestart"]);

    private static string? Query(string verb, string prefix, string name)
    {
        var (code, stdout, stderr) = ProcessRunner.Run(Dism, ["/English", "/Online", verb, prefix + Checked(name)], QueryTimeout);
        return ParseQuery(code, stdout + "\n" + stderr);
    }

    private static DismOutcome Change(string[] args)
    {
        var (code, stdout, stderr) = ProcessRunner.Run(Dism, new[] { "/English", "/Online" }.Concat(args).ToArray(), ChangeTimeout);
        return ClassifyChange(code, stdout + "\n" + stderr);
    }

    private static string Checked(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !NameRx.IsMatch(name))
            throw new ArgumentException($"Invalid DISM feature or capability name '{name}'", nameof(name));
        return name;
    }

    /// <summary>A failed run with error 5 / E_ACCESSDENIED, or error 740 (the process is not elevated).</summary>
    public static bool IsAccessDenied(int exitCode, string output) =>
        exitCode != 0 && exitCode != RestartExitCode
        && (exitCode is 5 or 740 or unchecked((int)0x80070005) or unchecked((int)0x800702E4)
            || Regex.IsMatch(output, @"Error:\s*(5|740|0x80070005|0x800702e4)\b", RegexOptions.IgnoreCase)
            || output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Only DISM's "unknown feature" result counts as not present: 0x800F080C (CBS_E_UNKNOWN_UPDATE, "Feature name ... is
    /// unknown"). Anything else, including 0x80070490 "Element not found", is a real failure. The result DISM gives for an
    /// unknown capability name is not documented; it is treated the same way only when it is this code or text (VM to confirm).
    /// </summary>
    public static bool IsNotFound(int exitCode, string output) =>
        exitCode != 0 && exitCode != RestartExitCode
        && (exitCode == unchecked((int)0x800F080C)
            || output.Contains("0x800f080c", StringComparison.OrdinalIgnoreCase)
            || output.Contains("is unknown", StringComparison.OrdinalIgnoreCase));

    /// <summary>The normalised state, null when the item does not exist. Throws for access denied and any other failure.</summary>
    public static string? ParseQuery(int exitCode, string output)
    {
        if (IsAccessDenied(exitCode, output)) throw new DismAccessDeniedException();
        if (exitCode != 0)
        {
            if (IsNotFound(exitCode, output)) return null;
            throw new InvalidOperationException($"DISM query failed (exit {exitCode}): {LastLines(output)}");
        }
        var m = StateRx.Match(output);
        if (!m.Success) throw new InvalidOperationException("DISM output has no State line");
        return NormaliseState(m.Groups[1].Value);
    }

    /// <summary>"Disable Pending" becomes "DisablePending"; an unknown state throws instead of being guessed.</summary>
    public static string NormaliseState(string raw)
    {
        var squeezed = Regex.Replace(raw.Trim(), @"\s+", "");
        foreach (var k in KnownStates)
            if (k.Equals(squeezed, StringComparison.OrdinalIgnoreCase)) return k;
        throw new InvalidOperationException($"Unknown DISM state '{raw.Trim()}'");
    }

    /// <summary>Done, RebootRequired, Deferred or NotPresent; access denied and every other failure throw with the DISM text.</summary>
    public static DismOutcome ClassifyChange(int exitCode, string output)
    {
        if (IsAccessDenied(exitCode, output)) throw new DismAccessDeniedException();
        if (exitCode == RestartExitCode) return DismOutcome.RebootRequired;
        if (exitCode == 0)
        {
            if (Regex.IsMatch(output, @"(Disable|Uninstall)\s+Pending", RegexOptions.IgnoreCase)) return DismOutcome.Deferred;
            return DismOutcome.Done;
        }
        if (IsNotFound(exitCode, output)) return DismOutcome.NotPresent;
        // The engine records the exception message, so the DISM text reaches the journal.
        throw new InvalidOperationException($"DISM failed (exit {exitCode}): {LastLines(output)}");
    }

    /// <summary>The DISM text from the "Error:" line on (up to 5 lines), else the last 3 lines.</summary>
    internal static string LastLines(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var err = Array.FindIndex(lines, l => l.StartsWith("Error", StringComparison.OrdinalIgnoreCase));
        return string.Join(" | ", err >= 0 ? lines.Skip(err).Take(5) : lines.TakeLast(3));
    }
}
