using System.Diagnostics;
using System.Security.Principal;

namespace NitrOS.Live.Windows;

/// <summary>Who NitrOS runs as, and who is signed in at the screen (the owner of explorer.exe in the console session).</summary>
public sealed class WinSessionFacts : ISessionFacts
{
    public string ProcessUserSid => WindowsIdentity.GetCurrent().User!.Value;

    public bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public int WindowsBuild => Environment.OSVersion.Version.Build;

    public string? ConsoleUserSid
    {
        get
        {
            var session = NativeMethods.WTSGetActiveConsoleSessionId();
            if (session == 0xFFFFFFFF) return null;
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                using (p)
                {
                    try
                    {
                        if (p.SessionId != session) continue;
                        var sid = OwnerSid(p);
                        if (sid != null) return sid;
                    }
                    catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                }
            }
            return null;
        }
    }

    private static string? OwnerSid(Process p)
    {
        if (!NativeMethods.OpenProcessToken(p.Handle, NativeMethods.TOKEN_QUERY, out var token)) return null;
        try
        {
            using var identity = new WindowsIdentity(token);
            return identity.User?.Value;
        }
        finally { NativeMethods.CloseHandle(token); }
    }
}
