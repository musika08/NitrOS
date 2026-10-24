using System.Text.Json;
using NitrOS.Core.Json;
using NitrOS.Core.Model;

namespace NitrOS.Core.Loading;

public static class CatalogLoader
{
    private const string CardsFile = "cards.json";

    public static CatalogLoadResult Load(string directory)
    {
        var errors = new List<CatalogError>();
        if (!Directory.Exists(directory))
        {
            errors.Add(new CatalogError(directory, null, null, "Directory not found."));
            return new CatalogLoadResult(null, errors);
        }

        CardMeasurements? cards = null;
        var cardsPath = Path.Combine(directory, CardsFile);
        if (!File.Exists(cardsPath))
            errors.Add(new CatalogError(CardsFile, null, null, "Required file is missing."));
        else
            cards = TryRead(cardsPath, CardsFile, CatalogJson.ParseCards, errors);

        List<string> names;
        try
        {
            names = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(p => Path.GetFileName(p))
                .Where(n => !string.Equals(n, CardsFile, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new CatalogError(directory, null, null, ex.Message));
            return new CatalogLoadResult(null, errors);
        }

        var files = new List<(string File, GroupFile Group)>();
        foreach (var name in names)
        {
            var group = TryRead(Path.Combine(directory, name), name, CatalogJson.ParseGroup, errors);
            if (group is null) continue;
            if (!CheckNullItems(name, group, errors)) continue;
            var tagged = group with
            {
                Switches = group.Switches.Select(s => s with { Group = group.Group }).ToList(),
            };
            files.Add((name, tagged));
        }

        errors.AddRange(CatalogValidator.Validate(files));

        if (errors.Count > 0 || cards is null)
            return new CatalogLoadResult(null, errors);
        return new CatalogLoadResult(new Catalog(files.Select(f => f.Group).ToList(), cards), errors);
    }

    /// <summary>
    /// System.Text.Json does not check collection elements against nullable annotations,
    /// so a null item would crash later stages. Returns false when the group has one.
    /// </summary>
    private static bool CheckNullItems(string file, GroupFile group, List<CatalogError> errors)
    {
        var ok = true;
        for (var i = 0; i < group.Switches.Count; i++)
        {
            var sw = group.Switches[i];
            if (sw is null)
            {
                errors.Add(new CatalogError(file, null, null, $"switches[{i}] is null."));
                ok = false;
                continue;
            }
            ok &= NoNulls(file, sw, "modes", sw.Modes, errors);
            ok &= NoNulls(file, sw, "live", sw.Live, errors);
            ok &= NoNulls(file, sw, "image", sw.Image, errors);
            ok &= NoNulls(file, sw, "breaks", sw.Breaks, errors);
            ok &= NoNulls(file, sw, "requires", sw.Requires, errors);
            ok &= NoNulls(file, sw, "skipOn", sw.SkipOn, errors);
        }
        return ok;
    }

    private static bool NoNulls<T>(string file, SwitchDef sw, string list, IReadOnlyList<T> items,
        List<CatalogError> errors)
    {
        var ok = true;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is not null) continue;
            errors.Add(new CatalogError(file, null, sw.Id, $"{list}[{i}] is null."));
            ok = false;
        }
        return ok;
    }

    private static T? TryRead<T>(string path, string name, Func<string, T> parse, List<CatalogError> errors)
        where T : class
    {
        try
        {
            return parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            errors.Add(new CatalogError(name, ex.LineNumber is { } l ? l + 1 : null, null, ex.Message));
        }
        catch (NotSupportedException ex)
        {
            errors.Add(new CatalogError(name, null, null, ex.Message));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new CatalogError(name, null, null, ex.Message));
        }
        return null;
    }
}
