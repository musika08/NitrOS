using System.Security.Cryptography;
using System.Text;
using NitrOS.Core.Json;
using NitrOS.Core.Model;

namespace NitrOS.Measure.Campaign;

public static class DefinitionHash
{
    public static string Of(SwitchDef s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var normalized = s with { Measured = null };
        var json = CatalogJson.Write(normalized);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public static string Of(IEnumerable<SwitchDef> switches)
    {
        ArgumentNullException.ThrowIfNull(switches);
        var normalizedList = switches.Select(s => s with { Measured = null }).ToList();
        var json = CatalogJson.Write(normalizedList);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
