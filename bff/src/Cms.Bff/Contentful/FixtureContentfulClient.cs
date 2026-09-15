using System.Text.Json;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Contentful;

public sealed class FixtureContentfulClient : IContentfulClient
{
    private readonly Lazy<CdaResponse> _fixture;
    private readonly ILogger<FixtureContentfulClient> _logger;

    public FixtureContentfulClient(IOptions<ContentfulOptions> options, IWebHostEnvironment env, ILogger<FixtureContentfulClient> logger)
    {
        _logger = logger;
        _fixture = new Lazy<CdaResponse>(() =>
        {
            var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.FixturePath));
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Fixture file not found at '{path}'. Set Contentful:FixturePath, or set Contentful:Mode=Live.", path);

            _logger.LogInformation("Contentful running in Fixture mode from {Path}", path);
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<CdaResponse>(stream) ?? new CdaResponse();
        });
    }

    public Task<CdaResponse> QueryAsync(ContentfulQuery query, bool preview, CancellationToken ct = default)
    {
        var all = _fixture.Value;

        // Everything not in Items is a candidate too: the fixture stores all entries in
        // includes, and any of them may be the subject of a query.
        var candidates = all.Items
            .Concat(all.Includes?.Entry ?? new())
            .Where(e => e.ContentTypeId == query.ContentType)
            .ToList();

        foreach (var (key, value) in query.Filters ?? new Dictionary<string, string>())
            candidates = candidates.Where(e => Matches(e, key, value)).ToList();

        if (query.Order is { Length: > 0 } order)
            candidates = ApplyOrder(candidates, order);

        // Preview mode is a no-op offline: fixtures contain no draft/published split.
        return Task.FromResult(new CdaResponse
        {
            Items = candidates.Take(query.Limit).ToList(),
            Includes = all.Includes,
            Total = candidates.Count,
        });
    }

    private static bool Matches(CdaEntry entry, string key, string value)
    {
        if (key == "sys.id") return entry.Sys.Id == value;

        if (!key.StartsWith("fields.", StringComparison.Ordinal))
            throw new NotSupportedException($"FixtureContentfulClient does not support filter key '{key}'.");

        var parts = key["fields.".Length..].Split('.');
        if (!entry.Fields.TryGetValue(parts[0], out var field)) return false;

        // fields.slug
        if (parts.Length == 1)
            return field.ValueKind == JsonValueKind.String && field.GetString() == value;

        // fields.category.sys.id
        if (parts is [_, "sys", "id"])
            return field.ValueKind == JsonValueKind.Object
                   && field.TryGetProperty("sys", out var sys)
                   && sys.TryGetProperty("id", out var id)
                   && id.GetString() == value;

        return false;
    }

    /// <summary>
    /// Mirrors Contentful's `order` query param syntax: a leading `-` means descending,
    /// otherwise ascending, e.g. "-fields.publishDate" or "fields.publishDate".
    /// </summary>
    private static List<CdaEntry> ApplyOrder(List<CdaEntry> candidates, string order)
    {
        var descending = order.StartsWith('-');
        var fieldPath = descending ? order[1..] : order;
        var fieldName = fieldPath.StartsWith("fields.", StringComparison.Ordinal)
            ? fieldPath["fields.".Length..]
            : fieldPath;

        // Entries missing the sort field sort last regardless of direction, so partition
        // them out before applying OrderBy/OrderByDescending rather than relying on the
        // comparer's null handling (which OrderByDescending would otherwise invert).
        var withKey = candidates.Select(e => (Entry: e, Key: SortKey(e, fieldName))).ToList();
        var missing = withKey.Where(x => x.Key is null).Select(x => x.Entry);
        var present = withKey.Where(x => x.Key is not null);

        present = descending
            ? present.OrderByDescending(x => x.Key, SortKeyComparer.Instance)
            : present.OrderBy(x => x.Key, SortKeyComparer.Instance);

        return present.Select(x => x.Entry).Concat(missing).ToList();
    }

    /// <summary>
    /// The sort key for a single field: a <see cref="DateTimeOffset"/> when the string value
    /// parses as one (e.g. "publishDate"), the raw string otherwise, or null when the field
    /// is absent or not a string.
    /// </summary>
    private static object? SortKey(CdaEntry entry, string fieldName)
    {
        if (!entry.Fields.TryGetValue(fieldName, out var field) || field.ValueKind != JsonValueKind.String)
            return null;

        var s = field.GetString();
        if (s is not null && DateTimeOffset.TryParse(s, out var dt))
            return dt;

        return s;
    }

    private sealed class SortKeyComparer : IComparer<object?>
    {
        public static readonly SortKeyComparer Instance = new();

        public int Compare(object? x, object? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            (DateTimeOffset dx, DateTimeOffset dy) => dx.CompareTo(dy),
            _ => string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal),
        };
    }
}
