using System.Collections;

namespace NitrOS.Measure.Host;

public static class Program
{
    public static int Main(string[] args)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
        {
            if (de.Key is string k)
            {
                env[k] = de.Value as string;
            }
        }

        return HostApp.Run(args, Console.Out, env);
    }
}
