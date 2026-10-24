using System.Text.Json;
using System.Text.Json.Serialization;
using NitrOS.Core.Model;

namespace NitrOS.Core.Json;

public static class CatalogJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            WriteIndented = true,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        return o;
    }

    public static GroupFile ParseGroup(string json) =>
        JsonSerializer.Deserialize<GroupFile>(json, Options)
        ?? throw new JsonException("Group file is null.");

    public static CardMeasurements ParseCards(string json) =>
        JsonSerializer.Deserialize<CardMeasurements>(json, Options)
        ?? throw new JsonException("Card measurements file is null.");

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
