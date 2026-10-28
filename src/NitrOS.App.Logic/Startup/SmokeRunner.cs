namespace NitrOS.App.Logic.Startup;

public sealed record SmokeReport(
    int CatalogSwitches,
    string Device,
    bool SessionOk,
    string? SessionReason,
    string MemoryLine,
    IReadOnlyList<string> Screens,
    bool ReviewGoEnabledTune,
    string? ReviewGoReasonIso,
    int Gamepads,
    bool? DpadMovedFocus);
