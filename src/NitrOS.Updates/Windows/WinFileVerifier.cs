using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using NitrOS.Live.Windows;

namespace NitrOS.Updates.Windows;

public sealed class WinFileVerifier : NitrOS.Updates.Installing.IFileVerifier
{
    public bool Sha256Matches(SafeFileHandle file, string expectedHex)
    {
        if (file == null || file.IsInvalid) return false;
        if (string.IsNullOrWhiteSpace(expectedHex)) return false;

        using var stream = new FileStream(file, FileAccess.Read, 4096, isAsync: false);
        stream.Seek(0, SeekOrigin.Begin);
        var hashBytes = SHA256.HashData(stream);
        var actualHex = Convert.ToHexString(hashBytes);

        return string.Equals(actualHex, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string>? Signers(SafeFileHandle file, string path)
    {
        return Authenticode.TrustedSignerNames(file, path, RevocationMode.WholeChainOnline);
    }
}
