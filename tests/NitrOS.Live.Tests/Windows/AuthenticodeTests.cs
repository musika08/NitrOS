namespace NitrOS.Live.Tests.Windows;

using System;
using System.IO;
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
    public void Authenticode_NullForUnsignedTempFile()
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
}
