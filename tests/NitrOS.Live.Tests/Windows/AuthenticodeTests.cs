namespace NitrOS.Live.Tests.Windows;

using System;
using System.IO;
using System.Linq;
using NitrOS.Live.Windows;

public sealed class AuthenticodeTests
{
    [Fact]
    public void Authenticode_SignedByMicrosoftForSystemDism()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var dismPath = Path.Combine(systemRoot, "System32", "dism.exe");
        Assert.True(File.Exists(dismPath), $"dism.exe must exist at {dismPath}");

        var names = Authenticode.TrustedSignerNames(dismPath);
        Assert.NotNull(names);
        Assert.NotEmpty(names);

        var isMs = Authenticode.SignedBy(dismPath, "Microsoft Corporation");
        Assert.True(isMs);
    }

    [Fact]
    public void Authenticode_SignedSystemFileHasMicrosoftSigner()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(systemRoot, "System32", "msiexec.exe");
        Assert.True(File.Exists(path), $"msiexec.exe must exist at {path}");

        // Test OfflineOnly
        var offlineNames = Authenticode.TrustedSignerNames(path, RevocationMode.OfflineOnly);
        Assert.NotNull(offlineNames);
        Assert.NotEmpty(offlineNames);
        Assert.Contains(offlineNames, n => Authenticode.NamesMatch(n, "Microsoft Windows") || Authenticode.NamesMatch(n, "Microsoft Corporation"));

        // Test WholeChainOnline
        var onlineNames = Authenticode.TrustedSignerNames(path, RevocationMode.WholeChainOnline);
        Assert.NotNull(onlineNames);
        Assert.NotEmpty(onlineNames);
        Assert.Contains(onlineNames, n => Authenticode.NamesMatch(n, "Microsoft Windows") || Authenticode.NamesMatch(n, "Microsoft Corporation"));
    }

    [Fact]
    public void Authenticode_UnsignedFileReturnsNull()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"nitros-auth-test-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "not signed");
            var names = Authenticode.TrustedSignerNames(tempFile);
            Assert.Null(names);

            var isMs = Authenticode.SignedBy(tempFile, "Microsoft Corporation");
            Assert.False(isMs);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void Authenticode_NamesMatchUnchanged()
    {
        Assert.True(Authenticode.NamesMatch("Valve Corp.", "Valve Corporation"));
        Assert.True(Authenticode.NamesMatch("ASUS", "ASUSTeK COMPUTER INC."));
        Assert.False(Authenticode.NamesMatch("Valve GmbH", "Valve Corporation"));
    }
}
