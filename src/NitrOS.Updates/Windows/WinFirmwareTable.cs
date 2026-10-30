namespace NitrOS.Updates.Windows;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using NitrOS.Live.Systems;
using NitrOS.Live.Windows;
using NitrOS.Updates.Model;
using NitrOS.Updates.Reading;

public sealed class WinFirmwareTable : IFirmwareTable
{
    private readonly IRegistry _registry;
    private readonly IInstalledDevices? _devices;

    public WinFirmwareTable(IRegistry? registry = null, IInstalledDevices? devices = null)
    {
        _registry = registry ?? new WinRegistry();
        _devices = devices ?? new WinInstalledDevices();
    }

    public IReadOnlyList<FirmwareResource> Read()
    {
        var list = new List<FirmwareResource>();
        const string esrtPath = @"HARDWARE\UEFI\ESRT";

        // Note: Registry subkey names under HKLM\HARDWARE\UEFI\ESRT are observed, not documented by Microsoft.
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default);
        using var key = hklm.OpenSubKey(esrtPath);
        if (key == null) return list;

        var subKeyNames = key.GetSubKeyNames();
        IReadOnlyList<InstalledDevice>? presentDevices = null;

        foreach (var subName in subKeyNames)
        {
            if (!Guid.TryParse(subName, out var guid)) continue;

            using var subKey = key.OpenSubKey(subName);
            if (subKey == null) continue;

            var type = (int)(subKey.GetValue("Type") as int? ?? (int?)Convert.ToInt64(subKey.GetValue("Type") ?? 0) ?? 0);
            var version = (uint)(subKey.GetValue("Version") as int? ?? (int?)Convert.ToInt64(subKey.GetValue("Version") ?? 0) ?? 0);
            var lastAttemptVersion = (uint)(subKey.GetValue("LastAttemptVersion") as int? ?? (int?)Convert.ToInt64(subKey.GetValue("LastAttemptVersion") ?? 0) ?? 0);
            var lastAttemptStatus = (uint)(subKey.GetValue("LastAttemptStatus") as int? ?? (int?)Convert.ToInt64(subKey.GetValue("LastAttemptStatus") ?? 0) ?? 0);

            // Join with present device UEFI\RES_{guid}\0
            presentDevices ??= _devices?.Present() ?? [];
            var matchId = $"UEFI\\RES_{guid:B}\\0";
            var matchedDev = presentDevices.FirstOrDefault(d => string.Equals(d.InstanceId, matchId, StringComparison.OrdinalIgnoreCase)
                                                                || d.InstanceId.Contains(guid.ToString(), StringComparison.OrdinalIgnoreCase));

            var pkgVer = matchedDev?.DriverVersion;
            var name = matchedDev?.Name;

            list.Add(new FirmwareResource(
                guid,
                type,
                version,
                lastAttemptVersion,
                lastAttemptStatus,
                pkgVer,
                name));
        }

        return list;
    }
}
