using NitrOS.Live.Measuring;

namespace NitrOS.App.Logic.Storage;

public sealed record HealthPending(string RunId, string UserSid, MachineSnapshot Before, DateTimeOffset At);

public sealed record AppState(HealthPending? Health, bool CheckAfterUpdates, string? LastCheckedBuild);
