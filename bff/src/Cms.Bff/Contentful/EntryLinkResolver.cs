using System.Text.Json;

namespace Cms.Bff.Contentful;

/// <summary>
/// Flattens a Contentful Delivery API response and resolves every Link node against
/// the includes block. Dangling links are dropped; cycles are cut with a stub entry.
/// </summary>
public sealed class EntryLinkResolver
{
    private const int MaxDepth = 12;

    public ResolvedEntry? Resolve(CdaResponse response, string entryId)
    {
        var (entries, assets) = BuildLookups(response);
        return entries.TryGetValue(entryId, out var entry)
            ? Convert(entry, entries, assets, new HashSet<string>(StringComparer.Ordinal), 0)
            : null;
    }

    public IReadOnlyList<ResolvedEntry> ResolveItems(CdaResponse response)
    {
        var (entries, assets) = BuildLookups(response);
        return response.Items
            .Select(item => Convert(item, entries, assets, new HashSet<string>(StringComparer.Ordinal), 0))
            .OfType<ResolvedEntry>()
            .ToList();
    }

    private static (Dictionary<string, CdaEntry> Entries, Dictionary<string, CdaEntry> Assets) BuildLookups(CdaResponse response)
    {
        var entries = new Dictionary<string, CdaEntry>(StringComparer.Ordinal);
        foreach (var e in response.Items) entries[e.Sys.Id] = e;
        foreach (var e in response.Includes?.Entry ?? new()) entries[e.Sys.Id] = e;

        var assets = new Dictionary<string, CdaEntry>(StringComparer.Ordinal);
        foreach (var a in response.Includes?.Asset ?? new()) assets[a.Sys.Id] = a;

        return (entries, assets);
    }

    private static ResolvedEntry? Convert(
        CdaEntry entry,
        Dictionary<string, CdaEntry> entries,
        Dictionary<string, CdaEntry> assets,
        HashSet<string> path,
        int depth)
    {
        // Cycle or depth limit: return a stub so consumers still see the reference,
        // without following it again.
        if (depth > MaxDepth || !path.Add(entry.Sys.Id))
            return new ResolvedEntry(entry.Sys.Id, entry.ContentTypeId, new Dictionary<string, object?>(), entry.Sys.UpdatedAt) { IsStub = true };

        try
        {
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, value) in entry.Fields)
                fields[name] = ConvertValue(value, entries, assets, path, depth);

            return new ResolvedEntry(entry.Sys.Id, entry.ContentTypeId, fields, entry.Sys.UpdatedAt);
        }
        finally
        {
            path.Remove(entry.Sys.Id);
        }
    }

    private static object? ConvertValue(
        JsonElement value,
        Dictionary<string, CdaEntry> entries,
        Dictionary<string, CdaEntry> assets,
        HashSet<string> path,
        int depth)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var s = value.GetString();
                // Known PoC limitation: a plain Symbol/Text field whose entire value happens
                // to be an exact YYYY-MM-DD-shaped string will be silently retyped as a date
                // rather than staying a string. Accepted narrow limitation, not a bug to fix.
                return LooksLikeDate(s) && DateTimeOffset.TryParse(s, out var dt) ? dt : s;

            case JsonValueKind.Number:
                return value.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetBoolean();

            case JsonValueKind.Null:
                return null;

            case JsonValueKind.Array:
                return value.EnumerateArray()
                    .Select(item => ConvertValue(item, entries, assets, path, depth))
                    .Where(item => item is not null)
                    .ToList();

            case JsonValueKind.Object:
                if (TryReadLink(value, out var linkType, out var linkId))
                {
                    if (linkType == "Asset")
                        return assets.TryGetValue(linkId, out var asset) ? ConvertAsset(asset) : null;

                    return entries.TryGetValue(linkId, out var linked)
                        ? Convert(linked, entries, assets, path, depth + 1)
                        : null; // dangling link: drop it
                }
                // RichText and Object fields pass through untouched.
                return value.Clone();

            default:
                return null;
        }
    }

    private static bool LooksLikeDate(string? s) =>
        s is { Length: >= 10 } && s[4] == '-' && s[7] == '-';

    private static bool TryReadLink(JsonElement value, out string linkType, out string linkId)
    {
        linkType = "";
        linkId = "";
        if (!value.TryGetProperty("sys", out var sys)) return false;
        if (!sys.TryGetProperty("type", out var type) || type.GetString() != "Link") return false;
        linkType = sys.TryGetProperty("linkType", out var lt) ? lt.GetString() ?? "" : "";
        linkId = sys.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
        return linkId.Length > 0;
    }

    private static ResolvedAsset? ConvertAsset(CdaEntry asset)
    {
        if (!asset.Fields.TryGetValue("file", out var file) || file.ValueKind != JsonValueKind.Object) return null;
        if (!file.TryGetProperty("url", out var urlProp)) return null;

        var url = urlProp.GetString() ?? "";
        if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;

        int? width = null, height = null;
        if (file.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object &&
            details.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object)
        {
            if (image.TryGetProperty("width", out var w) && w.ValueKind == JsonValueKind.Number) width = w.GetInt32();
            if (image.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number) height = h.GetInt32();
        }

        var title = asset.Fields.TryGetValue("title", out var t) ? t.GetString() : null;
        var contentType = file.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;

        return new ResolvedAsset(url, title, width, height, contentType);
    }
}
