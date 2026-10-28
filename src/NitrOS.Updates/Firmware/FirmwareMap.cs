using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NitrOS.Updates.Model;
using NitrOS.Updates.Versions;

namespace NitrOS.Updates.Firmware;

public enum CompareRule
{
    EsrtHex,
    PackageLastField
}

public sealed record FirmwareLink(FirmwareResource Resource, CompareRule Rule);

public sealed class FirmwareMap
{
    private readonly IReadOnlyList<ModelEntry> _models;

    public FirmwareMap(IReadOnlyList<ModelEntry> models)
    {
        _models = models ?? Array.Empty<ModelEntry>();
    }

    public static FirmwareMap Shipped()
    {
        var asm = typeof(FirmwareMap).Assembly;
        using var stream = asm.GetManifestResourceStream("NitrOS.Updates.Firmware.firmware-map.json")
            ?? throw new InvalidOperationException("Embedded resource 'firmware-map.json' not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return Parse(json);
    }

    public static FirmwareMap Parse(string json)
    {
        var doc = JsonSerializer.Deserialize<FirmwareMapDoc>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new FormatException("Invalid firmware map JSON.");

        var models = new List<ModelEntry>();
        if (doc.Models != null)
        {
            foreach (var m in doc.Models)
            {
                var maker = Enum.TryParse<MakerId>(m.Maker, ignoreCase: true, out var mk) ? mk : MakerId.Other;
                var items = new List<ItemMapping>();
                if (m.Items != null)
                {
                    foreach (var it in m.Items)
                    {
                        var rule = Enum.TryParse<CompareRule>(it.Rule, ignoreCase: true, out var r) ? r : CompareRule.EsrtHex;
                        items.Add(new ItemMapping(it.Title ?? string.Empty, it.Esrt ?? string.Empty, rule));
                    }
                }
                models.Add(new ModelEntry(maker, m.Model ?? string.Empty, items));
            }
        }

        return new FirmwareMap(models);
    }

    public FirmwareLink? ForListing(DeviceInfo device, string listingTitle)
    {
        var model = FindModel(device);
        if (model == null) return null;

        var mapping = model.Items.FirstOrDefault(i =>
            string.Equals(i.Title, listingTitle, StringComparison.OrdinalIgnoreCase) ||
            listingTitle.Contains(i.Title, StringComparison.OrdinalIgnoreCase));
        if (mapping == null) return null;

        var res = ResolveResource(device, mapping.Esrt);
        return res != null ? new FirmwareLink(res, mapping.Rule) : null;
    }

    public FirmwareLink? ForResource(DeviceInfo device, Guid esrtId)
    {
        var model = FindModel(device);
        if (model == null) return null;

        var res = device.Firmware.FirstOrDefault(f => f.Id == esrtId);
        if (res == null) return null;

        ItemMapping? mapping;
        if (res.Type == 1)
        {
            mapping = model.Items.FirstOrDefault(i => string.Equals(i.Esrt, "system", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            mapping = model.Items.FirstOrDefault(i =>
                Guid.TryParse(i.Esrt, out var g) && g == esrtId);
        }

        return mapping != null ? new FirmwareLink(res, mapping.Rule) : null;
    }

    public static bool RuleHolds(DeviceInfo device, FirmwareLink link)
    {
        if (link.Rule == CompareRule.EsrtHex)
        {
            if (link.Resource.Type == 1)
            {
                // EsrtHex on Type 1: TryEsrtHex(device.BiosVersion) equals Resource.Version
                return FirmwareNumber.TryEsrtHex(device.BiosVersion, out var hexNum) && hexNum == link.Resource.Version;
            }
            else
            {
                // EsrtHex on Type 2: TryEsrtHex(Resource.PackageVersion) equals Resource.Version
                if (string.IsNullOrEmpty(link.Resource.PackageVersion))
                    return false;
                return FirmwareNumber.TryEsrtHex(link.Resource.PackageVersion, out var hexNum) && hexNum == link.Resource.Version;
            }
        }
        else if (link.Rule == CompareRule.PackageLastField)
        {
            // PackageLastField: the package version parses (last field integer)
            return ParseLastField(link.Resource.PackageVersion).HasValue;
        }

        return false;
    }

    public static string? InstalledText(FirmwareLink link, DeviceInfo device)
    {
        if (!RuleHolds(device, link))
            return null;

        if (link.Rule == CompareRule.EsrtHex)
        {
            if (link.Resource.Type == 1)
            {
                // BIOS number, e.g. "319"
                return ExtractBiosNumber(device.BiosVersion);
            }
            else
            {
                // Hex of Version, padded to listing's width: "0015" (4 hex digits)
                return link.Resource.Version.ToString("X4", CultureInfo.InvariantCulture);
            }
        }
        else if (link.Rule == CompareRule.PackageLastField)
        {
            var lastField = ParseLastField(link.Resource.PackageVersion);
            return lastField?.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    public static int? CompareOffered(FirmwareLink link, DeviceInfo device, string offeredVersion)
    {
        if (!RuleHolds(device, link))
            return null;

        if (link.Rule == CompareRule.EsrtHex)
        {
            if (!FirmwareNumber.TryEsrtHex(offeredVersion, out var offeredHex))
                return null;
            return offeredHex.CompareTo(link.Resource.Version);
        }
        else if (link.Rule == CompareRule.PackageLastField)
        {
            var offeredNum = ParseLastField(offeredVersion);
            var installedNum = ParseLastField(link.Resource.PackageVersion);
            if (!offeredNum.HasValue || !installedNum.HasValue) return null;
            return offeredNum.Value.CompareTo(installedNum.Value);
        }

        return null;
    }

    private static uint? ParseLastField(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var dot = text.LastIndexOf('.');
        var token = dot >= 0 ? text[(dot + 1)..] : text;
        return uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string ExtractBiosNumber(string? biosVer)
    {
        if (string.IsNullOrEmpty(biosVer)) return string.Empty;
        var dot = biosVer.LastIndexOf('.');
        if (dot >= 0 && dot < biosVer.Length - 1)
        {
            var part = biosVer[(dot + 1)..];
            if (uint.TryParse(part, out _)) return part;
        }
        return biosVer;
    }

    private ModelEntry? FindModel(DeviceInfo device)
    {
        var modelCode = device.ModelCode;
        if (string.IsNullOrEmpty(modelCode)) return null;

        return _models.FirstOrDefault(m =>
            m.Maker == device.Maker &&
            string.Equals(m.Model, modelCode, StringComparison.OrdinalIgnoreCase));
    }

    private static FirmwareResource? ResolveResource(DeviceInfo device, string esrtKey)
    {
        if (string.Equals(esrtKey, "system", StringComparison.OrdinalIgnoreCase))
        {
            return device.Firmware.FirstOrDefault(f => f.Type == 1);
        }
        if (Guid.TryParse(esrtKey, out var g))
        {
            return device.Firmware.FirstOrDefault(f => f.Id == g);
        }
        return null;
    }

    public sealed record ModelEntry(MakerId Maker, string Model, IReadOnlyList<ItemMapping> Items);
    public sealed record ItemMapping(string Title, string Esrt, CompareRule Rule);

    private sealed class FirmwareMapDoc
    {
        public List<RawModel>? Models { get; set; }
    }

    private sealed class RawModel
    {
        public string? Maker { get; set; }
        public string? Model { get; set; }
        public List<RawItem>? Items { get; set; }
    }

    private sealed class RawItem
    {
        public string? Title { get; set; }
        public string? Esrt { get; set; }
        public string? Rule { get; set; }
    }
}
