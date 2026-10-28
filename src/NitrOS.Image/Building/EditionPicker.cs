namespace NitrOS.Image.Building;

using System;
using System.Collections.Generic;
using System.Linq;
using NitrOS.Image.Systems;

public static class EditionPicker
{
    public static ImageEdition? Pick(IReadOnlyList<ImageEdition> editions, int? requested)
    {
        ArgumentNullException.ThrowIfNull(editions);
        if (editions.Count == 0) return null;

        if (requested.HasValue)
        {
            var match = editions.FirstOrDefault(e => e.Index == requested.Value);
            if (match is not null) return match;
        }

        var pro = editions.FirstOrDefault(e => string.Equals(e.EditionId, "Professional", StringComparison.OrdinalIgnoreCase));
        if (pro is not null) return pro;

        return editions.FirstOrDefault(e => e.Index == 1) ?? editions[0];
    }

    public static string? Refusal(ImageEdition e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!string.Equals(e.Architecture, "x64", StringComparison.OrdinalIgnoreCase))
        {
            return "NitrOS builds x64 ISOs only.";
        }

        if (e.Version < BuildChecks.MinImage)
        {
            return "NitrOS needs a Windows 11 24H2 or newer ISO.";
        }

        return null;
    }
}
