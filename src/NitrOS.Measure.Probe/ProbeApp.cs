using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Loading;
using NitrOS.Live.Windows;
using NitrOS.Measure.Guest;

namespace NitrOS.Measure.Probe;

public static class ProbeApp
{
    public static int Run(
        string[] args,
        IGuestFacts facts,
        IKvpWriter kvp,
        Action<TimeSpan> sleep)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(kvp);
        ArgumentNullException.ThrowIfNull(sleep);

        if (args.Length == 0)
        {
            return 2;
        }

        var command = args[0];
        return command switch
        {
            "sample" => RunSample(args.Skip(1).ToArray(), facts, kvp, sleep),
            "fingerprint" => RunFingerprint(args.Skip(1).ToArray()),
            _ => 2,
        };
    }

    private static int RunSample(
        string[] args,
        IGuestFacts facts,
        IKvpWriter kvp,
        Action<TimeSpan> sleep)
    {
        string? outFile = null;
        int idleMinutes = 10;
        int count = 5;
        int intervalSeconds = 30;
        bool useKvp = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outFile = args[++i];
                    break;
                case "--idle-minutes" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out idleMinutes)) return 2;
                    break;
                case "--count" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out count)) return 2;
                    break;
                case "--interval-seconds" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out intervalSeconds)) return 2;
                    break;
                case "--kvp":
                    useKvp = true;
                    break;
                default:
                    return 2;
            }
        }

        if (string.IsNullOrEmpty(outFile))
        {
            return 2;
        }

        try
        {
            if (useKvp)
            {
                kvp.Set("NitrOS.Measure.State", "idle");
            }

            bool idleDone = false;
            Action<TimeSpan> wrappedSleep = span =>
            {
                sleep(span);
                if (!idleDone && useKvp)
                {
                    idleDone = true;
                    kvp.Set("NitrOS.Measure.State", "sampling");
                }
            };

            var options = new SamplerOptions(
                TimeSpan.FromMinutes(idleMinutes),
                count,
                TimeSpan.FromSeconds(intervalSeconds));

            var sampler = new Sampler(facts, wrappedSleep);
            var result = sampler.Run(options);

            var dir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outFile, CatalogJson.Write(result));

            if (useKvp)
            {
                kvp.Set("NitrOS.Measure.State", "done");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (useKvp)
            {
                try { kvp.Set("NitrOS.Measure.State", "error"); } catch { }
            }
            WriteError(outFile, ex);
            return 1;
        }
    }

    private static int RunFingerprint(string[] args)
    {
        string? outFile = null;
        string catalogDir = Path.Combine(AppContext.BaseDirectory, "catalog");

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outFile = args[++i];
                    break;
                case "--catalog" when i + 1 < args.Length:
                    catalogDir = args[++i];
                    break;
                default:
                    return 2;
            }
        }

        if (string.IsNullOrEmpty(outFile))
        {
            return 2;
        }

        try
        {
            var session = new WinSessionFacts();
            if (!session.IsElevated)
            {
                WriteError(outFile, new UnauthorizedAccessException("Fingerprint requires administrator elevation."));
                return 1;
            }

            var loadResult = CatalogLoader.Load(catalogDir);
            if (loadResult.Catalog is null)
            {
                throw new InvalidOperationException("Failed to load catalog: " + string.Join("; ", loadResult.Errors));
            }

            var systems = WinSystems.Create();
            var fingerprint = FingerprintTool.Capture(loadResult.Catalog, systems);

            var dir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(outFile, CatalogJson.Write(fingerprint));
            return 0;
        }
        catch (Exception ex)
        {
            WriteError(outFile, ex);
            return 1;
        }
    }

    private static void WriteError(string? outFile, Exception ex)
    {
        if (string.IsNullOrEmpty(outFile)) return;
        try
        {
            var errorFile = outFile + ".error.txt";
            File.WriteAllText(errorFile, ex.ToString());
        }
        catch { }
    }
}
