using NitrOS.Measure.Guest;

namespace NitrOS.Measure.Probe;

public static class Program
{
    public static int Main(string[] args)
    {
        return ProbeApp.Run(
            args,
            new WinGuestFacts(),
            new WinKvpWriter(),
            Thread.Sleep);
    }
}
