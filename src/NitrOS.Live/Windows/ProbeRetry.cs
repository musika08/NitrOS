namespace NitrOS.Live.Windows;

/// <summary>Brief re-checks for things that are still settling after a restart.</summary>
internal static class ProbeRetry
{
    public const int Attempts = 3;
    public static readonly TimeSpan Gap = TimeSpan.FromSeconds(2);

    /// <summary>Runs check up to three times, sleeping two seconds between tries. True as soon as one succeeds.</summary>
    public static bool Until(Func<bool> check, Action<TimeSpan> delay)
    {
        for (var i = 0; i < Attempts; i++)
        {
            if (check()) return true;
            if (i < Attempts - 1) delay(Gap);
        }
        return false;
    }
}
