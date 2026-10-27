namespace NitrOS.Measure.Guest;

public sealed class Sampler(IGuestFacts facts, Action<TimeSpan> sleep)
{
    public SampleRun Run(SamplerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var started = facts.Now();
        var unbiasedStart = facts.UnbiasedTime();

        sleep(options.Idle);

        var samples = new List<MemorySample>();

        for (int i = 0; i < options.Count; i++)
        {
            if (i > 0)
            {
                sleep(options.Interval);
            }

            var (total, avail) = facts.Memory();
            int inUseMb = (int)((total - avail) / (1024 * 1024));

            var procs = facts.Processes();
            int procCount = procs.Count(p => p.Pid != 0 && p.Pid != facts.OwnPid);
            int svchostCount = procs.Count(p => p.Pid != 0 && p.Pid != facts.OwnPid
                && string.Equals(p.Name, "svchost", StringComparison.OrdinalIgnoreCase));

            samples.Add(new MemorySample(facts.Now(), inUseMb, procCount, svchostCount));
        }

        var finished = facts.Now();
        var unbiasedEnd = facts.UnbiasedTime();

        var wallElapsed = finished - started;
        var unbiasedElapsed = unbiasedEnd - unbiasedStart;
        bool slept = (wallElapsed - unbiasedElapsed) > TimeSpan.FromSeconds(30);

        var (totalPhys, _) = facts.Memory();
        int visibleMb = (int)(totalPhys / (1024 * 1024));
        var build = facts.WindowsBuild();

        return new SampleRun(samples, visibleMb, build, slept, started, finished);
    }
}
