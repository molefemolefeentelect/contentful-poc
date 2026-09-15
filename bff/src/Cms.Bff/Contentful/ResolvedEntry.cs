using System.Text.Json;

namespace Cms.Bff.Contentful;

public sealed record ResolvedAsset(string Url, string? Title, int? Width, int? Height, string? ContentType);

/// <summary>
/// A Contentful entry with every Link node already resolved. Field values are one of:
/// string, bool, decimal, DateTimeOffset, ResolvedEntry, ResolvedAsset,
/// IReadOnlyList&lt;object?&gt;, or JsonElement (RichText and Object fields, passed through).
/// </summary>
public sealed record ResolvedEntry(
    string Id,
    string ContentTypeId,
    IReadOnlyDictionary<string, object?> Fields,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>
    /// True when this entry is a stub produced by cutting a circular reference or
    /// hitting the recursion depth limit — its Fields are intentionally empty because
    /// resolution was cut short, not because the entry genuinely has no content.
    /// </summary>
    public bool IsStub { get; init; }

    public object? Get(string field) => Fields.TryGetValue(field, out var v) ? v : null;

    public string? GetString(string field) => Get(field) as string;

    public bool GetBool(string field, bool fallback = false) => Get(field) is bool b ? b : fallback;

    public int? GetInt(string field) => Get(field) switch
    {
        decimal d when d >= int.MinValue && d <= int.MaxValue => (int)d,
        int i => i,
        _ => null,
    };

    public decimal? GetDecimal(string field) => Get(field) as decimal?;

    public DateTimeOffset? GetDate(string field) => Get(field) as DateTimeOffset?;

    public ResolvedEntry? GetEntry(string field) => Get(field) as ResolvedEntry;

    public ResolvedAsset? GetAsset(string field) => Get(field) as ResolvedAsset;

    public JsonElement? GetRichText(string field) => Get(field) as JsonElement?;

    public IReadOnlyList<ResolvedEntry> GetEntries(string field) =>
        Get(field) is IReadOnlyList<object?> list
            ? list.OfType<ResolvedEntry>().ToList()
            : Array.Empty<ResolvedEntry>();

    public IReadOnlyList<string> GetStrings(string field) =>
        Get(field) is IReadOnlyList<object?> list
            ? list.OfType<string>().ToList()
            : Array.Empty<string>();

    /// <summary>Every entry id reachable from this entry, used to build cache tags.</summary>
    public IReadOnlyCollection<string> CollectEntryIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Walk(this, ids);
        return ids;

        static void Walk(ResolvedEntry entry, HashSet<string> acc)
        {
            if (!acc.Add(entry.Id)) return;
            foreach (var value in entry.Fields.Values)
            {
                switch (value)
                {
                    case ResolvedEntry child:
                        Walk(child, acc);
                        break;
                    case IReadOnlyList<object?> list:
                        foreach (var item in list.OfType<ResolvedEntry>()) Walk(item, acc);
                        break;
                }
            }
        }
    }
}
