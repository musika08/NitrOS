using NitrOS.Live;

namespace NitrOS.App.Tests.Fakes;

public sealed class FakeSession(
    string processSid = "S-1-5-21-1",
    string? consoleSid = "S-1-5-21-1",
    bool elevated = true,
    int build = 26300) : ISessionFacts
{
    public string ProcessUserSid { get; set; } = processSid;
    public string? ConsoleUserSid { get; set; } = consoleSid;
    public bool IsElevated { get; set; } = elevated;
    public int WindowsBuild { get; set; } = build;
}
