using System.Diagnostics;

namespace NitrOS.Live.Windows;

/// <summary>Read-only checks: process list, service status, package query and one HTTP GET.</summary>
public sealed class WinHealthProbe : IHealthProbe
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly WinAppx _appx = new();

    public bool IsProcessRunning(string processName)
    {
        var all = Process.GetProcessesByName(processName);
        var running = all.Length > 0;
        foreach (var p in all) p.Dispose();
        return running;
    }

    public bool IsServiceRunning(string serviceName)
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
