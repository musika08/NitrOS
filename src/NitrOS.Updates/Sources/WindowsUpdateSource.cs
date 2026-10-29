using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;
using NitrOS.Updates.Versions;
using NitrOS.Updates.Windows;

namespace NitrOS.Updates.Sources;

public sealed class WindowsUpdateSource(IWindowsUpdateAgent agent, FirmwareMap map) : IUpdateSource
{
    private readonly IWindowsUpdateAgent _agent = agent ?? throw new ArgumentNullException(nameof(agent));
    private readonly FirmwareMap _map = map ?? throw new ArgumentNullException(nameof(map));

    public string Id => "windows-update";
    public string Name => "Windows Update";

    public bool AppliesTo(DeviceInfo device) => true;

    public async Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct)
    {
        IReadOnlyList<WuRecord> records;
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            records = await _agent.SearchDriversAsync(linkedCts.Token);
        }
        catch (Exception)
        {
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), UpdateTexts.WindowsUpdateUnavailable, null);
        }

        var items = new List<UpdateItem>();
        foreach (var rec in records)
        {
            var isFirmwareClass = string.Equals(rec.DriverClass, "Firmware", StringComparison.OrdinalIgnoreCase);
            var hwId = rec.DriverHardwareId ?? string.Empty;

            UpdateKind kind = UpdateKind.Driver;
            string key;
            string? installedVersion = null;
            string? compareRule = null;

            if (isFirmwareClass || hwId.StartsWith("UEFI\\RES_", StringComparison.OrdinalIgnoreCase))
            {
                // Parse Guid from UEFI\RES_{guid}
                Guid esrtGuid = Guid.Empty;
                var resPrefix = "UEFI\\RES_";
                if (hwId.StartsWith(resPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var guidStr = hwId[resPrefix.Length..];
                    var slash = guidStr.IndexOf('\\');
                    if (slash >= 0) guidStr = guidStr[..slash];
                    guidStr = guidStr.Trim('{', '}');
                    Guid.TryParse(guidStr, out esrtGuid);
                }

                var systemEsrt = device.Firmware.FirstOrDefault(f => f.Type == 1);
                var isSystemBios = systemEsrt != null && systemEsrt.Id == esrtGuid;

                kind = isSystemBios ? UpdateKind.Bios : UpdateKind.Firmware;
                key = esrtGuid != Guid.Empty ? $"fw:{esrtGuid}" : $"src:windows-update:{rec.UpdateId}";

                var link = _map.ForResource(device, esrtGuid);
                if (link != null)
                {
                    installedVersion = FirmwareMap.InstalledText(link, device);
                    compareRule = link.Rule.ToString();
                }
                else
                {
                    var fwRes = device.Firmware.FirstOrDefault(f => f.Id == esrtGuid);
                    installedVersion = fwRes?.PackageVersion;
                }
            }
            else
            {
                // Driver: find matching present device
                var matchedDevice = device.Devices.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(rec.DriverHardwareId) &&
                    (d.HardwareIds.Any(h => string.Equals(h, rec.DriverHardwareId, StringComparison.OrdinalIgnoreCase))));

                if (matchedDevice != null)
                {
                    key = $"dev:{matchedDevice.InstanceId}";
                    installedVersion = matchedDevice.DriverVersion;
                }
                else
                {
                    key = $"src:windows-update:{rec.UpdateId}";
                }
            }

            var latest = VersionText.FromTitle(rec.Title);
            DateOnly? relDate = rec.DriverVerDate.HasValue ? DateOnly.FromDateTime(rec.DriverVerDate.Value) : null;

            var item = new UpdateItem(
                key,
                rec.Title,
                kind,
                installedVersion,
                latest,
                null,
                rec.MaxDownloadSize,
                relDate,
                Id,
                Name)
            {
                Method = InstallMethod.WindowsUpdate,
                WindowsUpdateId = rec.UpdateId,
                RestartLikely = rec.RebootBehavior > 0,
                CompareRule = compareRule
            };

            items.Add(item);
        }

        return new SourceResult(SourceStatus.Ok, items, null, null);
    }
}
