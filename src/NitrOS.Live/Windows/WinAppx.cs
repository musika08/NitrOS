using NitrOS.Live.Systems;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace NitrOS.Live.Windows;

/// <summary>
/// PackageManager adapter. Names are matched against the package name (Package.Id.Name, for example
/// Microsoft.WindowsCalculator) with <see cref="Glob"/>. The all-users queries need an elevated process.
/// </summary>
public sealed class WinAppx : IAppx
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
    private readonly PackageManager _pm = new();

    public bool IsInstalled(string name) => InstalledForAllUsers(name).Count > 0;

    public bool IsProvisioned(string name) => Provisioned(name).Count > 0;

    public void RemoveForAllUsers(string name)
    {
        foreach (var (fullName, _) in InstalledForAllUsers(name))
            Wait(_pm.RemovePackageAsync(fullName, RemovalOptions.RemoveForAllUsers), $"removing {fullName}");
    }

    public void Deprovision(string name)
    {
        foreach (var family in Provisioned(name).Select(p => p.FamilyName).Distinct(StringComparer.OrdinalIgnoreCase))
            Wait(_pm.DeprovisionPackageForAllUsersAsync(family), $"deprovisioning {family}");
    }

    private List<(string FullName, string FamilyName)> InstalledForAllUsers(string name)
    {
        ValidateName(name);
        return Matching(_pm.FindPackages(), name);
    }

    private List<(string FullName, string FamilyName)> Provisioned(string name)
    {
        ValidateName(name);
        return Matching(_pm.FindProvisionedPackages(), name);
    }

    /// <summary>Read-only, current user only; needs no elevation. Used by the on-PC test.</summary>
    internal IReadOnlyList<string> InstalledForCurrentUser(string name)
    {
        ValidateName(name);
        return Matching(_pm.FindPackagesForUser(string.Empty), name).Select(p => p.FullName).ToList();
    }

    private static List<(string FullName, string FamilyName)> Matching(IEnumerable<global::Windows.ApplicationModel.Package> packages, string name)
    {
        var result = new List<(string, string)>();
        foreach (var p in packages)
        {
            var id = p.Id;
            if (Glob.IsMatch(name, id.Name)) result.Add((id.FullName, id.FamilyName));
        }
        return result;
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim('*').Length == 0)
            throw new ArgumentException($"Invalid package name '{name}': it must name something besides wildcards", nameof(name));
    }

    private static void Wait(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> op, string what)
    {
        var task = op.AsTask();
        if (!task.Wait(Timeout))
        {
            try { op.Cancel(); } catch (Exception) { /* best effort; the timeout is reported below */ }
            throw new TimeoutException($"Timed out after {Timeout.TotalMinutes} minutes {what}");
        }
        var r = task.Result;
        if (r.ExtendedErrorCode is { HResult: not 0 } err)
            throw new InvalidOperationException($"Failed {what}: {r.ErrorText} (0x{err.HResult:X8})", err);
    }
}
