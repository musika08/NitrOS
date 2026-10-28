namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NitrOS.Core.Json;
using NitrOS.Core.Model;

public sealed record BuildState(
    string BuildId,
    BuildRequest Request,
    string SelectionHash,
    SourceFingerprint Source,
    int EditionIndex,
    string? EditionName,
    BuildStep Next,
    IReadOnlyList<ImageEntry> Entries,
    string WorkFolder,
    DateTimeOffset Created,
    DateTimeOffset Updated)
{
    public static string NewBuildId(DateTimeOffset? now = null)
    {
        var timestamp = (now ?? DateTimeOffset.UtcNow).ToString("yyyyMMdd-HHmmss");
        var randomBytes = RandomNumberGenerator.GetBytes(2);
        var hex = Convert.ToHexStringLower(randomBytes);
        return $"{timestamp}{hex}";
    }
}

public static class SelectionHash
{
    public static string Of(IEnumerable<SwitchDef> switches)
    {
        ArgumentNullException.ThrowIfNull(switches);
        var normalizedList = switches
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .Select(s => s with { Measured = null })
            .ToList();

        var json = CatalogJson.Write(normalizedList);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
