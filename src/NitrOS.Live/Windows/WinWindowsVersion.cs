using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

public sealed class WinWindowsVersion(IRegistry registry) : IWindowsVersion
{
    private const string CurrentVersionKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
    private readonly IRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public WinWindowsVersion() : this(new WinRegistry()) { }

    public string FullBuild
    {
        get
        {
            var buildVal = _registry.GetValue(LiveHive.LocalMachine, CurrentVersionKey, "CurrentBuildNumber", RegView.Default);
            var buildStr = buildVal?.Data ?? "";

            var ubrVal = _registry.GetValue(LiveHive.LocalMachine, CurrentVersionKey, "UBR", RegView.Default);
            var ubrStr = ubrVal?.Data;

            if (string.IsNullOrEmpty(buildStr)) return "";
            return !string.IsNullOrEmpty(ubrStr) ? $"{buildStr}.{ubrStr}" : buildStr;
        }
    }
}
