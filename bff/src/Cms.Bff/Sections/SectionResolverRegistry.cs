using Cms.Bff.Contentful;

namespace Cms.Bff.Sections;

public sealed class SectionResolverRegistry
{
    private readonly Dictionary<string, ISectionResolver> _resolvers;
    private readonly ILogger<SectionResolverRegistry> _logger;

    public SectionResolverRegistry(IEnumerable<ISectionResolver> resolvers, ILogger<SectionResolverRegistry> logger)
    {
        _resolvers = resolvers.ToDictionary(r => r.ContentTypeId, StringComparer.Ordinal);
        _logger = logger;
    }

    public async Task<IReadOnlyList<SectionDto>> ResolveAllAsync(
        IEnumerable<ResolvedEntry> entries,
        SectionContext context,
        CancellationToken ct)
    {
        // Resolve in parallel (fund widgets call an upstream API) but restore author
        // ordering afterwards: the order the admin user set in Contentful is the
        // contract, and Task.WhenAll does not preserve it on its own.
        var ordered = entries.ToList();
        var tasks = ordered.Select(entry => ResolveOneAsync(entry, context, ct)).ToList();
        var results = await Task.WhenAll(tasks);

        return results.Where(s => s is not null).Select(s => s!).ToList();
    }

    private async Task<SectionDto?> ResolveOneAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        if (!_resolvers.TryGetValue(entry.ContentTypeId, out var resolver))
        {
            // Degradation contract: a content type the code does not know about is
            // dropped, never thrown. A half-migrated model must not 500 the page.
            _logger.LogWarning(
                "No resolver registered for section content type '{ContentType}' (entry {EntryId}) on page '{Slug}'. Section dropped.",
                entry.ContentTypeId, entry.Id, context.Slug);
            return null;
        }

        try
        {
            return await resolver.ResolveAsync(entry, context, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Resolver for '{ContentType}' failed on entry {EntryId} (page '{Slug}'). Section dropped.",
                entry.ContentTypeId, entry.Id, context.Slug);
            return null;
        }
    }
}
