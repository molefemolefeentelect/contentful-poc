using System.Text.Json;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

namespace Cms.Bff.Pages;

public sealed record ArticleResponse
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string? Excerpt { get; init; }
    public JsonElement? Body { get; init; }
    public ImageDto? FeaturedImage { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorJobTitle { get; init; }
    public string? CategoryName { get; init; }
    public string? CategorySlug { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTimeOffset? PublishDate { get; init; }
    public DateTimeOffset? UpdatedDate { get; init; }
    public required SeoDto Seo { get; init; }
    public IReadOnlyList<Dictionary<string, object?>> JsonLd { get; init; } = Array.Empty<Dictionary<string, object?>>();
    public IReadOnlyList<ArticleTeaserDto> Related { get; init; } = Array.Empty<ArticleTeaserDto>();
    public IReadOnlyCollection<string> EntryIds { get; init; } = Array.Empty<string>();
}
