using System.Text.Json;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.Model;

namespace NitrOS.Updates.Sources;

public sealed class AsusSource(HttpClient http, FirmwareMap map) : IUpdateSource
{
    internal const bool AsusDriverInstallsProven = false;

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly FirmwareMap _map = map ?? throw new ArgumentNullException(nameof(map));

    public string Id => "asus";
    public string Name => "ASUS";

    public bool AppliesTo(DeviceInfo device) => device.Maker == MakerId.Asus;

    public async Task<SourceResult> CheckAsync(DeviceInfo device, CancellationToken ct)
    {
        if (device.Maker != MakerId.Asus)
        {
            return new SourceResult(SourceStatus.NotForThisDevice, Array.Empty<UpdateItem>(), null, null);
        }

        var fallbackPage = SupportPages.For(MakerId.Asus, device.Model);

        var modelCode = device.ModelCode;
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), UpdateTexts.AsusNoModelCode, fallbackPage);
        }

        var biosUrl = $"https://rog.asus.com/support/webapi/product/GetPDBIOS?website=global&model={Uri.EscapeDataString(modelCode)}&cpu={Uri.EscapeDataString(modelCode)}&systemCode=rog";
        var driversUrl = $"https://rog.asus.com/support/webapi/product/GetPDDrivers?website=global&model={Uri.EscapeDataString(modelCode)}&cpu={Uri.EscapeDataString(modelCode)}&osid=52&systemCode=rog";

        AsusApiResponse? biosResponse;
        AsusApiResponse? driversResponse;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var biosJsonTask = _http.GetStringAsync(biosUrl, linkedCts.Token);
            var driversJsonTask = _http.GetStringAsync(driversUrl, linkedCts.Token);
            await Task.WhenAll(biosJsonTask, driversJsonTask);

            biosResponse = JsonSerializer.Deserialize<AsusApiResponse>(biosJsonTask.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            driversResponse = JsonSerializer.Deserialize<AsusApiResponse>(driversJsonTask.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            var problem = string.Format(UpdateTexts.AsusUnavailableFormat, ex.Message);
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), problem, fallbackPage);
        }

        if (biosResponse?.Status != "SUCCESS" || driversResponse?.Status != "SUCCESS")
        {
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), UpdateTexts.AsusShapeChanged, fallbackPage);
        }

        if (biosResponse.Result?.Obj == null && driversResponse.Result?.Obj == null)
        {
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), UpdateTexts.AsusShapeChanged, fallbackPage);
        }

        var items = new List<UpdateItem>();
        ProcessCategories(biosResponse.Result?.Obj, device, items);
        ProcessCategories(driversResponse.Result?.Obj, device, items);

        if (items.Count == 0)
        {
            var problem = string.Format(UpdateTexts.AsusNoUsableFilesFormat, modelCode);
            return new SourceResult(SourceStatus.Unavailable, Array.Empty<UpdateItem>(), problem, fallbackPage);
        }

        return new SourceResult(SourceStatus.Ok, items, null, null);
    }

    private void ProcessCategories(List<AsusCategoryObj>? categories, DeviceInfo device, List<UpdateItem> result)
    {
        if (categories == null) return;

        foreach (var cat in categories)
        {
            if (string.Equals(cat.Name, "OS Assistant", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cat.Files == null) continue;

            foreach (var file in cat.Files)
            {
                var title = file.Title?.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                // Ruling 6: Drop unwanted software & store link rows
                if (title.StartsWith("Armoury Crate", StringComparison.OrdinalIgnoreCase) &&
                    !title.Contains("Control Interface", StringComparison.OrdinalIgnoreCase))
                {
                    // Note: Keep Armoury Crate Control Interface if it is a driver, but drop installers
                    if (title.Contains("Installer", StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                if (title.StartsWith("MyASUS", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (title.StartsWith("GlideX", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (title.StartsWith("Dolby", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (title.Contains("EZ Flash", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Validate download URL
                var globalUrl = file.DownloadUrl?.Global;
                if (string.IsNullOrWhiteSpace(globalUrl)) continue;
                if (!Uri.TryCreate(globalUrl, UriKind.Absolute, out var uri)) continue;
                if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "dlcdnets.asus.com", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Kind
                UpdateKind kind;
                if (string.Equals(cat.Name, "BIOS Update(Windows)", StringComparison.OrdinalIgnoreCase))
                {
                    kind = UpdateKind.Bios;
                }
                else if (string.Equals(cat.Name, "Firmware", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("Firmware", StringComparison.OrdinalIgnoreCase) ||
                         title.Contains("MCU", StringComparison.OrdinalIgnoreCase))
                {
                    kind = UpdateKind.Firmware;
                }
                else
                {
                    kind = UpdateKind.Driver;
                }

                // Key & Installed
                string key;
                string? installed = null;
                string? compareRule = null;

                if (kind == UpdateKind.Bios || kind == UpdateKind.Firmware)
                {
                    var link = _map.ForListing(device, title);
                    if (link != null)
                    {
                        key = $"fw:{link.Resource.Id}";
                        installed = FirmwareMap.InstalledText(link, device);
                        compareRule = link.Rule.ToString();
                    }
                    else
                    {
                        key = $"src:asus:{title}";
                    }
                }
                else
                {
                    // Driver: match by HardwareInfoList
                    InstalledDevice? matchedDevice = null;
                    if (file.HardwareInfoList != null)
                    {
                        foreach (var hw in file.HardwareInfoList)
                        {
                            if (string.IsNullOrWhiteSpace(hw.HardwareId)) continue;
                            matchedDevice = device.Devices.FirstOrDefault(d =>
                                d.HardwareIds.Any(h => string.Equals(h, hw.HardwareId, StringComparison.OrdinalIgnoreCase)));
                            if (matchedDevice != null) break;
                        }
                    }

                    if (matchedDevice != null)
                    {
                        key = $"dev:{matchedDevice.InstanceId}";
                        installed = matchedDevice.DriverVersion;
                    }
                    else
                    {
                        key = $"src:asus:{title}";
                    }
                }

                // Method & silent args
                string? silentArgs = null;
                if (!string.IsNullOrEmpty(file.ExeModule))
                {
                    var pctIdx = file.ExeModule.IndexOf("%%", StringComparison.Ordinal);
                    if (pctIdx >= 0)
                    {
                        silentArgs = file.ExeModule[(pctIdx + 2)..].Trim();
                    }
                }

                InstallMethod method;
                if (kind == UpdateKind.Driver)
                {
                    method = (AsusDriverInstallsProven && !string.IsNullOrEmpty(silentArgs))
                        ? InstallMethod.MakerPackage
                        : InstallMethod.HandOff;
                }
                else
                {
                    method = InstallMethod.HandOff;
                }

                var size = ListingParse.SizeBytes(file.FileSize);
                var relDate = ListingParse.AsusDate(file.ReleaseDate);
                var reboot = file.Reboot == "1";

                var item = new UpdateItem(
                    key,
                    title,
                    kind,
                    installed,
                    file.Version?.TrimStart('V', 'v'),
                    uri,
                    size,
                    relDate,
                    Id,
                    Name)
                {
                    Sha256 = file.Sha256,
                    SilentArgs = silentArgs,
                    Method = method,
                    RestartLikely = reboot,
                    CompareRule = compareRule
                };

                result.Add(item);
            }
        }
    }
}
