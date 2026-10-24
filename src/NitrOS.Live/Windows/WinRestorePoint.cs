using System.ComponentModel;
using System.Globalization;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>
/// Creates a restore point with SRSetRestorePointW (APPLICATION_INSTALL, BEGIN then END_SYSTEM_CHANGE). Windows makes
/// at most one point per 24 hours and reports success without one; that is detected by comparing the newest restore
/// point time (WMI root\default:SystemRestore) before and after. Windows' frequency setting is never changed.
/// </summary>
public sealed class WinRestorePoint : IRestorePoint
{
    private static readonly TimeSpan WmiTimeout = TimeSpan.FromMinutes(2);

    // Fixed text: no caller input reaches the command line.
    private const string NewestPointScript =
        "$ErrorActionPreference='Stop'; " +
        "Get-CimInstance -Namespace root/default -ClassName SystemRestore | " +
        "ForEach-Object { $t = $_.CreationTime; " +
        "if ($t -isnot [datetime]) { $t = [Management.ManagementDateTimeConverter]::ToDateTime([string]$t) }; " +
        "$t.ToUniversalTime().ToString('o') }";

    public RestorePointOutcome Create(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("A restore point needs a description", nameof(description));
        var text = description.Length > 255 ? description[..255] : description;
        // A failed read of the restore point list never stops the run or counts as "System Restore is off".
        DateTime? before = null;
        var beforeKnown = true;
        try { before = NewestPointTime(); }
        catch (Exception) { beforeKnown = false; }

        var begin = new NativeMethods.RESTOREPOINTINFOW
        {
            dwEventType = NativeMethods.BEGIN_SYSTEM_CHANGE,
            dwRestorePtType = NativeMethods.APPLICATION_INSTALL,
            llSequenceNumber = 0,
            szDescription = text,
        };
        if (!NativeMethods.SRSetRestorePointW(ref begin, out var status))
        {
            if (status.nStatus == NativeMethods.ERROR_SERVICE_DISABLED) return RestorePointOutcome.Unavailable;
            throw new Win32Exception((int)status.nStatus, "SRSetRestorePointW (begin) failed");
        }

        var end = new NativeMethods.RESTOREPOINTINFOW
        {
            dwEventType = NativeMethods.END_SYSTEM_CHANGE,
            dwRestorePtType = NativeMethods.APPLICATION_INSTALL,
            llSequenceNumber = status.llSequenceNumber,
            szDescription = text,
        };
        if (!NativeMethods.SRSetRestorePointW(ref end, out var endStatus))
            throw new Win32Exception((int)endStatus.nStatus, "SRSetRestorePointW (end) failed");

        return Finish(before, beforeKnown, NewestPointTime);
    }

    /// <summary>
    /// The outcome once Windows accepted the request. <paramref name="before"/> is the newest point time read beforehand, or
    /// null with <paramref name="beforeKnown"/> false when that read failed.
    /// </summary>
    internal static RestorePointOutcome Finish(DateTime? before, bool beforeKnown, Func<DateTime?> readAfter)
    {
        DateTime? after;
        try { after = readAfter(); }
        catch (Exception) { return RestorePointOutcome.CreatedUnknown; } // the request went through; only the check failed
        return beforeKnown ? Classify(before, after) : RestorePointOutcome.CreatedUnknown;
    }

    /// <summary>A newer point after the call means one was made; otherwise Windows skipped it (one per 24 hours).</summary>
    public static RestorePointOutcome Classify(DateTime? newestBefore, DateTime? newestAfter) =>
        newestAfter is { } after && (newestBefore is not { } b || after > b)
            ? RestorePointOutcome.Created
            : RestorePointOutcome.SkippedRecent;

    private static DateTime? NewestPointTime()
    {
        var ps = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
        var (code, stdout, stderr) = ProcessRunner.Run(ps, ["-NoProfile", "-NonInteractive", "-Command", NewestPointScript], WmiTimeout);
        if (code != 0) throw new InvalidOperationException($"Reading restore points failed (exit {code}): {stderr.Trim()}");
        return ParseNewest(stdout);
    }

    /// <summary>The latest of the ISO 8601 UTC times, one per line; null when there are none. A bad line throws.</summary>
    public static DateTime? ParseNewest(string output)
    {
        DateTime? newest = null;
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var t = DateTime.Parse(line, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
            if (newest is null || t > newest) newest = t;
        }
        return newest;
    }
}
