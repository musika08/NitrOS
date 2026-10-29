using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using NitrOS.Live.Windows;

namespace NitrOS.Updates.Windows;

public sealed class WinFileVerifier : NitrOS.Updates.Installing.IFileVerifier
{
    public bool Sha256Matches(SafeFileHandle file, string expectedHex)
    {
        if (file == null || file.IsInvalid || file.IsClosed) return false;
        if (string.IsNullOrWhiteSpace(expectedHex)) return false;

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        long offset = 0;
        int bytesRead;

        while ((bytesRead = RandomAccess.Read(file, buffer, offset)) > 0)
        {
            sha.AppendData(buffer, 0, bytesRead);
            offset += bytesRead;
        }

        var hashBytes = sha.GetHashAndReset();
        var actualHex = Convert.ToHexString(hashBytes);

        return string.Equals(actualHex, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string>? Signers(SafeFileHandle file, string path)
    {
        return Authenticode.TrustedSignerNames(file, path, RevocationMode.WholeChainOnline);
    }
}
