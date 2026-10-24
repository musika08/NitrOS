using System.Diagnostics;

namespace NitrOS.Live.Windows;

/// <summary>Read-only checks: process list, service status, package query and one HTTP GET.</summary>
public sealed class WinHealthProbe : IHealthProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly WinAppx _appx = new();
    private readonly Action<TimeSpan> _delay;

    public WinHealthProbe() : this(Thread.Sleep) { }

    /// <summary>The delay is injectable so tests do not sleep.</summary>
    internal WinHealthProbe(Action<TimeSpan> delay) => _delay = delay;

    public bool IsProcessRunning(string processName)
    {
        // Only this user's session counts: another session's process says nothing about ours.
        using var me = Process.GetCurrentProcess();
        var session = me.SessionId;
        return ProbeRetry.Until(() => InSession(processName, session), _delay);
    }

    private static bool InSession(string processName, int sessionId)
    {
        var all = Process.GetProcessesByName(processName);
        try { return all.Any(p => p.SessionId == sessionId); }
        finally { foreach (var p in all) p.Dispose(); }
    }

    // SERVICE_START_PENDING is "not yet", so it is re-checked like any other not-running state.
    public bool IsServiceRunning(string serviceName) => ProbeRetry.Until(() => ServiceRunningOnce(serviceName), _delay);

    private static bool ServiceRunningOnce(string serviceName)
    {
        using var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm.IsInvalid) return false;
        using var svc = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_QUERY_STATUS);
        if (svc.IsInvalid) return false;
        return NativeMethods.QueryServiceStatus(svc, out var st) && st.dwCurrentState == NativeMethods.SERVICE_RUNNING;
    }

    public bool IsPackageInstalled(string packageName)
    {
        try { return _appx.IsInstalled(packageName); }
        catch (UnauthorizedAccessException) { return _appx.InstalledForCurrentUser(packageName).Count > 0; }
    }

    public string? HttpGet(string url)
    {
        try { return Http.GetStringAsync(url).GetAwaiter().GetResult(); }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { return null; }
    }
}
