using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live;

public record XboxRestoreResult(bool Ok, IReadOnlyList<(string Part, bool Ok, string? Detail)> Parts);

public interface IXboxRestoreSteps
{
    bool RestoreStore(IProgress<string>? progress, CancellationToken ct);
    bool RestoreXboxSignIn(IProgress<string>? progress, CancellationToken ct);
    bool RestoreGameBar(IProgress<string>? progress, CancellationToken ct);
    bool RestoreGamingServices(IProgress<string>? progress, CancellationToken ct);
    bool RestoreXboxApp(IProgress<string>? progress, CancellationToken ct);
}

public interface IXboxRestore
{
    XboxRestoreResult Run(IProgress<string>? progress, CancellationToken ct);
}

public sealed class XboxRestore(LiveSystems systems, IXboxRestoreSteps steps, Catalog? catalog = null) : IXboxRestore
{
    private static readonly string[] DefaultServices = ["XblAuthManager", "XblGameSave", "XboxNetApiSvc"];

    public XboxRestoreResult Run(IProgress<string>? progress, CancellationToken ct)
    {
        var services = catalog?.Switches.FirstOrDefault(s => s.Id == "xbox-services-off")?.Live
            .OfType<ServiceStart>()
            .Select(a => a.Service)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? DefaultServices.ToList();

        // 1. Turn services back to Windows default (Manual / Demand Start)
        foreach (var service in services)
        {
            progress?.Report($"Setting service {service} to Manual...");
            systems.Services.Set(service, ServiceStartType.Manual);
        }

        if (ct.IsCancellationRequested)
        {
            return CheckParts(services);
        }

        // 2. Run steps
        progress?.Report("Restoring Microsoft Store...");
        steps.RestoreStore(progress, ct);
        if (ct.IsCancellationRequested) return CheckParts(services);

        progress?.Report("Restoring Xbox Identity Provider...");
        steps.RestoreXboxSignIn(progress, ct);
        if (ct.IsCancellationRequested) return CheckParts(services);

        progress?.Report("Restoring Xbox Game Bar...");
        steps.RestoreGameBar(progress, ct);
        if (ct.IsCancellationRequested) return CheckParts(services);

        progress?.Report("Restoring Gaming Services...");
        steps.RestoreGamingServices(progress, ct);
        if (ct.IsCancellationRequested) return CheckParts(services);

        progress?.Report("Restoring Xbox App...");
        steps.RestoreXboxApp(progress, ct);

        // 3. Check parts
        return CheckParts(services);
    }

    private XboxRestoreResult CheckParts(IReadOnlyList<string> services)
    {
        var parts = new List<(string Part, bool Ok, string? Detail)>();

        var badServices = new List<string>();
        foreach (var service in services)
        {
            var info = systems.Services.Get(service);
            if (info is null || info.Start == ServiceStartType.Disabled)
            {
                badServices.Add(service);
            }
        }

        var servicesOk = badServices.Count == 0;
        parts.Add(("Xbox Services", servicesOk, servicesOk ? null : $"Disabled: {string.Join(", ", badServices)}"));

        void CheckApp(string displayName, string package)
        {
            var installed = systems.Appx.IsInstalled(package);
            parts.Add((displayName, installed, installed ? null : $"{package} not installed"));
        }

        CheckApp("Microsoft Store", "Microsoft.WindowsStore");
        CheckApp("Xbox Identity Provider", "Microsoft.XboxIdentityProvider");
        CheckApp("Xbox Game Bar", "Microsoft.XboxGamingOverlay");
        CheckApp("Gaming Services", "Microsoft.GamingServices");
        CheckApp("Xbox App", "Microsoft.GamingApp");

        var allOk = parts.All(p => p.Ok);
        return new XboxRestoreResult(allOk, parts);
    }
}
