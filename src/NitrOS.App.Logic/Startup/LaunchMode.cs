namespace NitrOS.App.Logic.Startup;

public abstract record LaunchMode
{
    public sealed record NormalMode : LaunchMode;
    public sealed record AfterRestartMode : LaunchMode;
    public sealed record AfterUpdateMode : LaunchMode;
    public sealed record SmokeMode(string ResultPath) : LaunchMode;

    public static readonly LaunchMode Normal = new NormalMode();
    public static readonly LaunchMode AfterRestart = new AfterRestartMode();
    public static readonly LaunchMode AfterUpdate = new AfterUpdateMode();
    public static LaunchMode Smoke(string resultPath) => new SmokeMode(resultPath);

    public static LaunchMode Parse(string[] args)
    {
        if (args is null || args.Length == 0) return Normal;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--after-restart", StringComparison.OrdinalIgnoreCase))
                return AfterRestart;
            if (string.Equals(args[i], "--after-update", StringComparison.OrdinalIgnoreCase))
                return AfterUpdate;
            if (string.Equals(args[i], "--smoke", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return Smoke(args[i + 1]);
        }
        return Normal;
    }
}
