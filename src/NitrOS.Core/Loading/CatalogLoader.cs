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

        var names = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(p => Path.GetFileName(p))
            .Where(n => !string.Equals(n, CardsFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var files = new List<(string File, GroupFile Group)>();
        foreach (var name in names)
        {
            var group = TryRead(Path.Combine(directory, name), name, CatalogJson.ParseGroup, errors);
            if (group is null) continue;
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
