using Microsoft.Win32.SafeHandles;

namespace NitrOS.Updates.Installing;

public interface IFileVerifier
{
    bool Sha256Matches(SafeFileHandle file, string expectedHex);
    IReadOnlyList<string>? Signers(SafeFileHandle file, string path);
}

public interface IPackageRunner
{
    int Run(string path, SafeFileHandle lockedFile, string? arguments, TimeSpan timeout);
    void StartInteractive(string path, SafeFileHandle lockedFile);
}

public interface IUnelevatedLauncher
{
    bool OpenPage(Uri page);
    bool ShowInFolder(string path);
}
