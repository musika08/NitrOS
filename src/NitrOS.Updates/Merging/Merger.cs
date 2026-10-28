using System.Globalization;
using NitrOS.Updates.Firmware;
using NitrOS.Updates.KnownBad;
using NitrOS.Updates.Model;
using NitrOS.Updates.Versions;

namespace NitrOS.Updates.Merging;

public static class Merger
{
    public static IReadOnlyList<MergedItem> Merge(
        IReadOnlyList<UpdateItem> items,
        DeviceInfo device,
        KnownBadList knownBad,
        FirmwareMap map)
    {
        if (items == null || items.Count == 0)
            return Array.Empty<MergedItem>();

        var groups = items.GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase);
        var merged = new List<MergedItem>();

        foreach (var group in groups)
        {
            var key = group.Key;
            var isFw = key.StartsWith("fw:", StringComparison.OrdinalIgnoreCase);
            var isDev = key.StartsWith("dev:", StringComparison.OrdinalIgnoreCase);

            var candidateItems = new List<UpdateItem>();
            string? latestBlockedReason = null;
            UpdateItem? newestBlockedItem = null;

            foreach (var item in group)
            {
                var bad = knownBad.Find(device.Maker, device.ModelCode, item.Kind, item.LatestVersion ?? string.Empty);
                if (bad != null)
                {
                    if (newestBlockedItem == null || CompareItems(item, newestBlockedItem, isFw, device, map) > 0)
                    {
                        newestBlockedItem = item;
                        latestBlockedReason = bad.Reason;
                    }
                }
                else
                {
                    candidateItems.Add(item);
                }
            }

            if (candidateItems.Count == 0)
            {
                // All items blocked
                var representative = newestBlockedItem ?? group.First();
                var also = group.Where(i => !ReferenceEquals(i, representative)).ToList();
                merged.Add(new MergedItem(representative, ItemState.Blocked, latestBlockedReason, also));
                continue;
            }

            // Find winner among candidateItems
            // Ranking algorithm: highest LatestVersion by group's comparison:
            // For fw: FirmwareMap.CompareOffered translated into ESRT numbers; otherwise VersionText.Compare.
            // Ties go to source id "windows-update", then to earlier source in candidate order.
            // Items whose versions do not compare are kept in AlsoOfferedBy, never chosen over a comparable one.
            var winner = candidateItems[0];
            for (int i = 1; i < candidateItems.Count; i++)
            {
                var current = candidateItems[i];
                var cmp = CompareItems(current, winner, isFw, device, map);
                if (cmp > 0)
                {
                    winner = current;
                }
                else if (cmp == 0)
                {
                    if (string.Equals(current.SourceId, "windows-update", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(winner.SourceId, "windows-update", StringComparison.OrdinalIgnoreCase))
                    {
                        winner = current;
                    }
                }
                else if (cmp == null)
                {
                    // If current is unparseable and winner is parseable, winner stays.
                    // If current is parseable and winner is unparseable, current wins.
                    if (IsComparable(current, isFw, device, map) && !IsComparable(winner, isFw, device, map))
                    {
                        winner = current;
                    }
                }
            }

            var alsoOffered = group.Where(i => !ReferenceEquals(i, winner)).ToList();

            // Determine State:
            // PageOnly when winner's Method is PageOnly
            // CantTell when installed is null or comparison is null
            // UpToDate when offered <= installed
            // else NewVersion
            ItemState state;
            if (winner.Method == InstallMethod.PageOnly)
            {
                state = ItemState.PageOnly;
            }
            else
            {
                var cmpWithInstalled = CompareWithInstalled(winner, isFw, isDev, device, map);
                if (cmpWithInstalled == null)
                {
                    state = ItemState.CantTell;
                }
                else if (cmpWithInstalled.Value <= 0)
                {
                    state = ItemState.UpToDate;
                }
                else
                {
                    state = ItemState.NewVersion;
                }
            }

            merged.Add(new MergedItem(winner, state, null, alsoOffered));
        }

        // Output order: NewVersion, then CantTell, Blocked, PageOnly, UpToDate;
        // within each, Bios, Firmware, Driver; then name.
        return merged
            .OrderBy(m => StateOrder(m.State))
            .ThenBy(m => KindOrder(m.Item.Kind))
            .ThenBy(m => m.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<MergedItem> CombineWithDevices(
        IReadOnlyList<MergedItem> updateItems,
        DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var result = new List<MergedItem>(updateItems ?? Array.Empty<MergedItem>());
        var additional = new List<MergedItem>();

        // 1. BIOS
        if (!string.IsNullOrWhiteSpace(device.BiosVersion) &&
            !result.Any(m => m.Item.Kind == UpdateKind.Bios))
        {
            var biosName = $"{device.Manufacturer} {device.Model} BIOS".Trim();
            if (string.IsNullOrWhiteSpace(device.Manufacturer) && string.IsNullOrWhiteSpace(device.Model))
                biosName = "System BIOS";

            var biosItem = new UpdateItem(
                "bios",
                biosName,
                UpdateKind.Bios,
                device.BiosVersion,
                device.BiosVersion,
                null,
                null,
                device.BiosDate,
                "system",
                "Installed")
            {
                Method = InstallMethod.PageOnly
            };
            additional.Add(new MergedItem(biosItem, ItemState.UpToDate, null, Array.Empty<UpdateItem>()));
        }

        // 2. Firmware (ESRT)
        foreach (var fw in device.Firmware)
        {
            var fwKey = $"fw:{fw.Id}";
            if (!result.Any(m => string.Equals(m.Item.Key, fwKey, StringComparison.OrdinalIgnoreCase)))
            {
                var fwName = fw.Name ?? "System Firmware";
                var fwVer = fw.PackageVersion ?? (fw.Version > 0 ? fw.Version.ToString() : null);
                var fwItem = new UpdateItem(
                    fwKey,
                    fwName,
                    UpdateKind.Firmware,
                    fwVer,
                    fwVer,
                    null,
                    null,
                    null,
                    "system",
                    "Installed")
                {
                    Method = InstallMethod.PageOnly
                };
                additional.Add(new MergedItem(fwItem, ItemState.UpToDate, null, Array.Empty<UpdateItem>()));
            }
        }

        // 3. Hardware Devices (Display, Net, Media, Bluetooth)
        var interestingClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Display", "Net", "Media", "Bluetooth"
        };

        foreach (var dev in device.Devices)
        {
            if (string.IsNullOrWhiteSpace(dev.DriverVersion) || string.IsNullOrWhiteSpace(dev.Name))
                continue;

            if (dev.DeviceClass == null || !interestingClasses.Contains(dev.DeviceClass))
                continue;

            // Skip noisy virtual / WAN / miniport devices
            var name = dev.Name;
            if (name.StartsWith("WAN Miniport", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft Kernel", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("NDIS Virtual", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Bluetooth Device (Personal Area Network)", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Generic software", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if this device is already represented in result
            bool alreadyRepresented = result.Any(m =>
                string.Equals(m.Item.Key, $"dev:{dev.InstanceId}", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Item.Name, dev.Name, StringComparison.OrdinalIgnoreCase) ||
                (dev.HardwareIds.Count > 0 && dev.HardwareIds.Any(hid => m.Item.Key.EndsWith(hid, StringComparison.OrdinalIgnoreCase))));

            if (!alreadyRepresented)
            {
                var devItem = new UpdateItem(
                    $"dev:{dev.InstanceId}",
                    dev.Name,
                    UpdateKind.Driver,
                    dev.DriverVersion,
                    dev.DriverVersion,
                    null,
                    null,
                    dev.DriverDate,
                    "system",
                    dev.DriverProvider ?? "Installed")
                {
                    Method = InstallMethod.PageOnly
                };
                additional.Add(new MergedItem(devItem, ItemState.UpToDate, null, Array.Empty<UpdateItem>()));
            }
        }

        result.AddRange(additional
            .OrderBy(m => KindOrder(m.Item.Kind))
            .ThenBy(m => m.Item.Name, StringComparer.OrdinalIgnoreCase));

        return result;
    }

    private static int StateOrder(ItemState s) => s switch
    {
        ItemState.NewVersion => 0,
        ItemState.CantTell => 1,
        ItemState.Blocked => 2,
        ItemState.PageOnly => 3,
        ItemState.UpToDate => 4,
        _ => 5
    };

    private static int KindOrder(UpdateKind k) => k switch
    {
        UpdateKind.Bios => 0,
        UpdateKind.Firmware => 1,
        UpdateKind.Driver => 2,
        _ => 3
    };

    private static bool IsComparable(UpdateItem item, bool isFw, DeviceInfo device, FirmwareMap map)
    {
        if (string.IsNullOrEmpty(item.LatestVersion)) return false;

        if (isFw)
        {
            var link = ResolveLink(item, device, map);
            if (link == null) return false;
            return ToEsrtNumber(item.LatestVersion, link) != null;
        }

        return VersionText.TryParse(item.LatestVersion, out _);
    }

    private static int? CompareItems(UpdateItem a, UpdateItem b, bool isFw, DeviceInfo device, FirmwareMap map)
    {
        if (isFw)
        {
            var linkA = ResolveLink(a, device, map);
            var linkB = ResolveLink(b, device, map);
            var numA = linkA != null && a.LatestVersion != null ? ToEsrtNumber(a.LatestVersion, linkA) : null;
            var numB = linkB != null && b.LatestVersion != null ? ToEsrtNumber(b.LatestVersion, linkB) : null;

            if (numA.HasValue && numB.HasValue)
            {
                return numA.Value.CompareTo(numB.Value);
            }
            if (numA.HasValue && !numB.HasValue) return 1;
            if (!numA.HasValue && numB.HasValue) return -1;
            return null;
        }

        var parsedA = a.LatestVersion != null && VersionText.TryParse(a.LatestVersion, out _);
        var parsedB = b.LatestVersion != null && VersionText.TryParse(b.LatestVersion, out _);

        if (parsedA && parsedB)
        {
            return VersionText.Compare(a.LatestVersion, b.LatestVersion);
        }
        if (parsedA && !parsedB) return 1;
        if (!parsedA && parsedB) return -1;
        return null;
    }

    private static int? CompareWithInstalled(UpdateItem item, bool isFw, bool isDev, DeviceInfo device, FirmwareMap map)
    {
        if (isFw)
        {
            if (string.IsNullOrEmpty(item.LatestVersion)) return null;

            var link = ResolveLink(item, device, map);
            if (link == null)
            {
                // If not found in map, or cannot resolve link, check if we have installed version on item
                if (string.IsNullOrEmpty(item.InstalledVersion)) return null;
                return VersionText.Compare(item.LatestVersion, item.InstalledVersion);
            }

            return FirmwareMap.CompareOffered(link, device, item.LatestVersion);
        }

        // Driver or other item: compare against installed
        var installed = item.InstalledVersion;
        if (string.IsNullOrEmpty(installed) && isDev)
        {
            // Try lookup installed from device.Drivers matching device key
            var devId = item.Key[4..]; // strip "dev:"
            var devMatch = device.Devices.FirstOrDefault(d =>
                string.Equals(d.InstanceId, devId, StringComparison.OrdinalIgnoreCase) ||
                d.HardwareIds.Any(hid => string.Equals(hid, devId, StringComparison.OrdinalIgnoreCase)));
            installed = devMatch?.DriverVersion;
        }

        if (string.IsNullOrEmpty(installed))
        {
            return null;
        }

        return VersionText.Compare(item.LatestVersion, installed);
    }

    private static FirmwareLink? ResolveLink(UpdateItem item, DeviceInfo device, FirmwareMap map)
    {
        if (item.Key.StartsWith("fw:", StringComparison.OrdinalIgnoreCase))
        {
            var esrtPart = item.Key[3..];
            if (Guid.TryParse(esrtPart, out var g))
            {
                var link = map.ForResource(device, g);
                if (link != null) return link;
            }
        }

        return map.ForListing(device, item.Name);
    }

    private static uint? ToEsrtNumber(string version, FirmwareLink link)
    {
        if (link.Rule == CompareRule.EsrtHex)
        {
            return FirmwareNumber.TryEsrtHex(version, out var hex) ? hex : null;
        }
        else if (link.Rule == CompareRule.PackageLastField)
        {
            var dot = version.LastIndexOf('.');
            var token = dot >= 0 ? version[(dot + 1)..] : version;
            return uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        }
        return null;
    }
}
