namespace NitrOS.Image.Windows;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class Privileges
{
    public static void Enable(params string[] privilegeNames)
    {
        ArgumentNullException.ThrowIfNull(privilegeNames);

        var currentProcess = Process.GetCurrentProcess().Handle;
        if (!ImageNativeMethods.OpenProcessToken(currentProcess, ImageNativeMethods.TOKEN_ADJUST_PRIVILEGES | ImageNativeMethods.TOKEN_QUERY, out var tokenHandle))
        {
            throw new UnauthorizedAccessException("NitrOS needs administrator rights to build an ISO.");
        }

        using (tokenHandle)
        {
            foreach (var priv in privilegeNames)
            {
                if (!ImageNativeMethods.LookupPrivilegeValueW(null, priv, out var luid))
                {
                    continue;
                }

                var tp = new ImageNativeMethods.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privilege = new ImageNativeMethods.LUID_AND_ATTRIBUTES
                    {
                        Luid = luid,
                        Attributes = ImageNativeMethods.SE_PRIVILEGE_ENABLED
                    }
                };

                if (!ImageNativeMethods.AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new UnauthorizedAccessException("NitrOS needs administrator rights to build an ISO.");
                }

                if (Marshal.GetLastWin32Error() == ImageNativeMethods.ERROR_NOT_ALL_ASSIGNED)
                {
                    throw new UnauthorizedAccessException("NitrOS needs administrator rights to build an ISO.");
                }
            }
        }
    }
}
