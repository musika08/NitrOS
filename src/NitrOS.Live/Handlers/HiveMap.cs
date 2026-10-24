using NitrOS.Live.Systems;

namespace NitrOS.Live.Handlers;

internal static class HiveMap
{
    public static LiveHive Parse(string hive) => hive.ToUpperInvariant() switch
    {
        "HKLM" => LiveHive.LocalMachine,
        "HKCU" => LiveHive.CurrentUser,
        _ => throw new ArgumentException($"Unknown registry hive '{hive}'", nameof(hive)),
    };

    public static string Name(LiveHive h) => h == LiveHive.LocalMachine ? "HKLM" : "HKCU";
}
