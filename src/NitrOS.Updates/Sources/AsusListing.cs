using System.Text.Json.Serialization;

namespace NitrOS.Updates.Sources;

public sealed class AsusApiResponse
{
    [JsonPropertyName("Status")]
    public string? Status { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("Result")]
    public AsusResultObj? Result { get; set; }
}

public sealed class AsusResultObj
{
    [JsonPropertyName("Count")]
    public int Count { get; set; }

    [JsonPropertyName("Obj")]
    public List<AsusCategoryObj>? Obj { get; set; }
}

public sealed class AsusCategoryObj
{
    [JsonPropertyName("Name")]
    public string? Name { get; set; }

    [JsonPropertyName("Count")]
    public int Count { get; set; }

    [JsonPropertyName("Files")]
    public List<AsusFileObj>? Files { get; set; }
}

public sealed class AsusFileObj
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("Title")]
    public string? Title { get; set; }

    [JsonPropertyName("FileSize")]
    public string? FileSize { get; set; }

    [JsonPropertyName("ReleaseDate")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("DownloadUrl")]
    public AsusDownloadUrls? DownloadUrl { get; set; }

    [JsonPropertyName("ExeModule")]
    public string? ExeModule { get; set; }

    [JsonPropertyName("Reboot")]
    public string? Reboot { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("HardwareInfoList")]
    public List<AsusHardwareInfo>? HardwareInfoList { get; set; }
}

public sealed class AsusDownloadUrls
{
    [JsonPropertyName("Global")]
    public string? Global { get; set; }

    [JsonPropertyName("China")]
    public string? China { get; set; }
}

public sealed class AsusHardwareInfo
{
    [JsonPropertyName("hardwareid")]
    public string? HardwareId { get; set; }
}
