using Cms.Bff.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Caching;

/// <summary>
/// Caches resolved payloads and keeps the tag index in step. Preview requests never
/// reach this: drafts must not be shared between requests.
/// </summary>
public sealed class PageCache
{
    private readonly IMemoryCache _cache;
    private readonly CacheTagStore _tags;
    private readonly ContentfulOptions _options;
    private readonly ILogger<PageCache> _logger;

    public PageCache(IMemoryCache cache, CacheTagStore tags, IOptions<ContentfulOptions> options, ILogger<PageCache> logger)
    {
        _cache = cache;
        _tags = tags;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        bool preview,
        Func<Task<(T Value, IReadOnlyCollection<string> EntryIds)>> factory)
    {
        if (preview) return (await factory()).Value;

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var (value, entryIds) = await factory();

        _cache.Set(cacheKey, value, TimeSpan.FromSeconds(_options.CacheSeconds));
        _tags.Associate(cacheKey, entryIds);

        return value;
    }

    /// <summary>Evicts every cached payload embedding the given entry. Returns the keys evicted.</summary>
    public IReadOnlyCollection<string> InvalidateEntry(string entryId)
    {
        var keys = _tags.KeysFor(entryId);

        foreach (var key in keys)
        {
            _cache.Remove(key);
            _tags.Forget(key);
        }

        _logger.LogInformation("Entry {EntryId} changed: evicted {Count} cached payload(s).", entryId, keys.Count);
        return keys;
    }
}
