using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace NitrOS.Live.Windows;

/// <summary>Who NitrOS runs as, and who is signed in at the screen (the user of the active console session).</summary>
public sealed class WinSessionFacts : ISessionFacts
{
    public string ProcessUserSid
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return id.User!.Value;
        }
    }

    public bool IsElevated
    {
        get
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public int WindowsBuild
    {
        get
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out var build)) return build;
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
            return Environment.OSVersion.Version.Build;
        }
    }

    public string? ConsoleUserSid
    {
        get
        {
            var session = NativeMethods.WTSGetActiveConsoleSessionId();
            if (session == 0xFFFFFFFF) return null;
            var user = Query(session, NativeMethods.WTSUserName);
            if (string.IsNullOrEmpty(user)) return null;
            var domain = Query(session, NativeMethods.WTSDomainName);
            try
            {
                var account = string.IsNullOrEmpty(domain) ? new NTAccount(user) : new NTAccount(domain, user);
                return ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
            }
            catch (IdentityNotMappedException) { return null; }
        }
    }

    private static string? Query(uint session, int infoClass)
    {
        if (!NativeMethods.WTSQuerySessionInformation(IntPtr.Zero, session, infoClass, out var buffer, out _)) return null;
        try { return Marshal.PtrToStringUni(buffer); }
        finally { NativeMethods.WTSFreeMemory(buffer); }
    }
}
