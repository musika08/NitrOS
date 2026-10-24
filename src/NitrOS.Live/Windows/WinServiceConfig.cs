using System.ComponentModel;
using System.Globalization;
using Microsoft.Win32;
using NitrOS.Core.Model;
using NitrOS.Live.Systems;

namespace NitrOS.Live.Windows;

/// <summary>
/// Reads the start type from HKLM\SYSTEM\CurrentControlSet\Services\&lt;name&gt; (Start, DelayedAutostart) and sets it
/// through the service control manager, changing nothing but the start type (and the delayed flag for automatic).
/// </summary>
public sealed class WinServiceConfig : IServiceConfig
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public ServiceStartInfo? Get(string name)
    {
        ValidateName(name);
        using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = b.OpenSubKey(ServicesKey + "\\" + name);
        if (k is null) return null;
        var start = k.GetValue("Start");
        var delayed = k.GetValue("DelayedAutostart");
        return new ServiceStartInfo(FromRegistry(start, delayed, name));
    }

    /// <summary>Maps the registry Start and DelayedAutostart values. Anything unexpected throws instead of guessing.</summary>
    public static ServiceStartType FromRegistry(object? start, object? delayed, string name)
    {
        if (start is not int s) throw new InvalidOperationException($"Service '{name}' has no valid Start value");
        var isDelayed = delayed switch
        {
            null => false,
            int d => d != 0,
            _ => throw new InvalidOperationException($"Service '{name}' has an invalid DelayedAutostart value"),
        };
        return s switch
        {
            0 => ServiceStartType.Boot,
            1 => ServiceStartType.System,
            2 => isDelayed ? ServiceStartType.AutomaticDelayed : ServiceStartType.Automatic,
            3 => ServiceStartType.Manual,
            4 => ServiceStartType.Disabled,
            _ => throw new InvalidOperationException($"Service '{name}' has an unknown Start value {s.ToString(CultureInfo.InvariantCulture)}"),
        };
    }

    /// <summary>The SERVICE_*_START value for ChangeServiceConfigW.</summary>
    public static uint ToStartType(ServiceStartType t) => t switch
    {
        ServiceStartType.Boot => 0,
        ServiceStartType.System => 1,
        ServiceStartType.Automatic or ServiceStartType.AutomaticDelayed => 2,
        ServiceStartType.Manual => 3,
        ServiceStartType.Disabled => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, "Unknown service start type"),
    };

    public void Set(string name, ServiceStartType start)
    {
        ValidateName(name);
        var startType = ToStartType(start);
        using var scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm.IsInvalid) throw new Win32Exception($"OpenSCManager failed for '{name}'");
        using var svc = NativeMethods.OpenService(scm, name, NativeMethods.SERVICE_CHANGE_CONFIG | NativeMethods.SERVICE_QUERY_CONFIG);
        if (svc.IsInvalid) throw new Win32Exception($"OpenService failed for '{name}'");

        if (!NativeMethods.ChangeServiceConfig(svc, NativeMethods.SERVICE_NO_CHANGE, startType, NativeMethods.SERVICE_NO_CHANGE,
                null, null, IntPtr.Zero, null, null, null, null))
            throw new Win32Exception($"ChangeServiceConfig failed for '{name}'");

        // The delayed flag only means something for automatic start; clear it for plain automatic so a read gives back what was set.
        if (start is ServiceStartType.Automatic or ServiceStartType.AutomaticDelayed)
        {
            var info = new NativeMethods.SERVICE_DELAYED_AUTO_START_INFO { fDelayedAutostart = start == ServiceStartType.AutomaticDelayed ? 1 : 0 };
            if (!NativeMethods.ChangeServiceConfig2(svc, NativeMethods.SERVICE_CONFIG_DELAYED_AUTO_START_INFO, ref info))
                throw new Win32Exception($"ChangeServiceConfig2 (delayed auto-start) failed for '{name}'");
        }
    }

    public IReadOnlyList<string> PerUserInstances(string template)
    {
        ValidateName(template);
        using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var k = b.OpenSubKey(ServicesKey) ?? throw new InvalidOperationException("The Services registry key is missing");
        return MatchInstances(template, k.GetSubKeyNames());
    }

    /// <summary>Per-user instances are named &lt;template&gt;_&lt;hex suffix&gt; (for example CDPUserSvc_4a3b2).</summary>
    public static IReadOnlyList<string> MatchInstances(string template, IEnumerable<string> serviceNames)
    {
        var prefix = template + "_";
        return serviceNames
            .Where(n => n.Length > prefix.Length
                && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && n.AsSpan(prefix.Length).ToString().All(Uri.IsHexDigit))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Service names cannot contain slashes; a backslash here would walk to another registry key.</summary>
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(['\\', '/']) >= 0 || name.Length > 256)
            throw new ArgumentException($"Invalid service name '{name}'", nameof(name));
    }
}
