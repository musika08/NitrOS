using Microsoft.Win32.SafeHandles;
using NitrOS.Live.Systems;
using NitrOS.Updates.Installing;

namespace NitrOS.Updates.Tests.Fakes;

public sealed class FakeVerifier : IFileVerifier
{
    public bool ShaMatchesResult { get; set; } = true;
    public List<string>? SignerNamesResult { get; set; } = ["ASUSTeK COMPUTER INC."];

    public bool Sha256Matches(SafeFileHandle file, string expectedHex) => ShaMatchesResult;

    public IReadOnlyList<string>? Signers(SafeFileHandle file, string path) => SignerNamesResult;
}

public sealed class FakeRunner : IPackageRunner
{
    public int ExitCodeToReturn { get; set; } = 0;
    public List<(string Path, string? Args)> RunCalls { get; } = [];
    public List<string> InteractiveCalls { get; } = [];

    public int Run(string path, SafeFileHandle lockedFile, string? arguments, TimeSpan timeout)
    {
        RunCalls.Add((path, arguments));
        return ExitCodeToReturn;
    }

    public void StartInteractive(string path, SafeFileHandle lockedFile)
    {
        InteractiveCalls.Add(path);
    }
}

public sealed class FakeLauncher : IUnelevatedLauncher
{
    public List<Uri> OpenedPages { get; } = [];
    public List<string> ShownFolders { get; } = [];
    public bool ResultToReturn { get; set; } = true;

    public bool OpenPage(Uri page)
    {
        OpenedPages.Add(page);
        return ResultToReturn;
    }

    public bool ShowInFolder(string path)
    {
        ShownFolders.Add(path);
        return ResultToReturn;
    }
}
