namespace NitrOS.Live.Windows;

using System;
using System.Collections.Generic;
using System.Linq;

public static class Authenticode
{
    public static IReadOnlyList<string>? TrustedSignerNames(string path) =>
        WinUninstaller.TrustedSignerNames(path);

    public static bool SignedBy(string path, string organisation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organisation);
        var signers = TrustedSignerNames(path);
        if (signers is null || signers.Count == 0) return false;

        return signers.Any(s => s.Contains(organisation, StringComparison.OrdinalIgnoreCase));
    }
}
