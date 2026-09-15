using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cms.Bff.Contentful;

public sealed class CdaLinkSys
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("linkType")] public string? LinkType { get; set; }
}

public sealed class CdaLink
{
    [JsonPropertyName("sys")] public CdaLinkSys Sys { get; set; } = new();
}

public sealed class CdaSys
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("createdAt")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("contentType")] public CdaLink? ContentType { get; set; }
}

public sealed class CdaEntry
{
    [JsonPropertyName("sys")] public CdaSys Sys { get; set; } = new();
    [JsonPropertyName("fields")] public Dictionary<string, JsonElement> Fields { get; set; } = new();

    public string ContentTypeId => Sys.ContentType?.Sys.Id ?? "";
}

public sealed class CdaIncludes
{
    [JsonPropertyName("Entry")] public List<CdaEntry> Entry { get; set; } = new();
    [JsonPropertyName("Asset")] public List<CdaEntry> Asset { get; set; } = new();
}

public sealed class CdaResponse
{
    [JsonPropertyName("items")] public List<CdaEntry> Items { get; set; } = new();
    [JsonPropertyName("includes")] public CdaIncludes? Includes { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}
