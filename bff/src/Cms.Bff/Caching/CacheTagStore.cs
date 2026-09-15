using System.Collections.Concurrent;

namespace Cms.Bff.Caching;

/// <summary>
/// Reverse index from Contentful entry id to the cache keys whose payload embeds it.
/// Lets a webhook naming one entry evict exactly the affected pages.
/// </summary>
public sealed class CacheTagStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _entryToKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyToEntries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Associate(string cacheKey, IEnumerable<string> entryIds)
    {
        lock (_gate)
        {
            RemoveKeyLocked(cacheKey);

            var ids = new HashSet<string>(entryIds, StringComparer.Ordinal);
            _keyToEntries[cacheKey] = ids;

            foreach (var id in ids)
            {
                var keys = _entryToKeys.GetOrAdd(id, _ => new HashSet<string>(StringComparer.Ordinal));
                keys.Add(cacheKey);
            }
        }
    }

    public IReadOnlyCollection<string> KeysFor(string entryId)
    {
        lock (_gate)
        {
            return _entryToKeys.TryGetValue(entryId, out var keys)
                ? keys.ToArray()
                : Array.Empty<string>();
        }
    }

    public void Forget(string cacheKey)
    {
        lock (_gate)
        {
            RemoveKeyLocked(cacheKey);
        }
    }

    private void RemoveKeyLocked(string cacheKey)
    {
        if (!_keyToEntries.TryRemove(cacheKey, out var previous)) return;

        foreach (var id in previous)
        {
            if (!_entryToKeys.TryGetValue(id, out var keys)) continue;
            keys.Remove(cacheKey);
            if (keys.Count == 0) _entryToKeys.TryRemove(id, out _);
        }
    }
}
