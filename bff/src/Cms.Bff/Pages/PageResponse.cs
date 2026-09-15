using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

namespace Cms.Bff.Pages;

public sealed record PageResponse
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string PageType { get; init; }
    public required SeoDto Seo { get; init; }
    public IReadOnlyList<Dictionary<string, object?>> JsonLd { get; init; } = Array.Empty<Dictionary<string, object?>>();
    public IReadOnlyList<BreadcrumbDto> Breadcrumbs { get; init; } = Array.Empty<BreadcrumbDto>();
    public IReadOnlyList<SectionDto> Sections { get; init; } = Array.Empty<SectionDto>();
    public DateTimeOffset? UpdatedAt { get; init; }
    /// <summary>Contentful entry ids this payload embeds, for cache tagging and Next.js revalidation tags.</summary>
    public IReadOnlyCollection<string> EntryIds { get; init; } = Array.Empty<string>();
}
