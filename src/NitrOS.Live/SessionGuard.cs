namespace NitrOS.Live;

public interface ISessionFacts
{
    string ProcessUserSid { get; }
    string? ConsoleUserSid { get; }
    bool IsElevated { get; }
    int WindowsBuild { get; }
}

public record SessionCheck(bool Ok, string? Reason);

/// <summary>Stops NitrOS from changing the wrong user's settings or running on unsupported Windows.</summary>
public static class SessionGuard
{
    public const int MinBuild = 26100; // Windows 11 24H2

    public static SessionCheck Check(ISessionFacts facts)
    {
        if (facts.WindowsBuild < MinBuild) return Refuse("NitrOS needs Windows 11 24H2 or newer.");
        if (!facts.IsElevated) return Refuse("NitrOS needs administrator rights.");
        if (string.IsNullOrEmpty(facts.ConsoleUserSid)) return Refuse("No one is signed in at this PC.");
        if (!string.Equals(facts.ProcessUserSid, facts.ConsoleUserSid, StringComparison.OrdinalIgnoreCase))
            return Refuse("NitrOS is running as a different user than the one signed in, so it would change the wrong account's settings.");
        return new SessionCheck(true, null);
    }

    private static SessionCheck Refuse(string reason) => new(false, reason);
}
