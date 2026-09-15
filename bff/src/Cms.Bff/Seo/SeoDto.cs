using System.Text.Json;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo;

public sealed record SeoDto
{
    public required string MetaTitle { get; init; }
    public required string MetaDescription { get; init; }
    public required string OgTitle { get; init; }
    public required string OgDescription { get; init; }
    public ImageDto? OgImage { get; init; }
    public required string CanonicalUrl { get; init; }
    public bool NoIndex { get; init; }
    public bool NoFollow { get; init; }
    public string StructuredDataType { get; init; } = "WebPage";
    public JsonElement? StructuredDataOverrides { get; init; }

    /// <summary>Ready-to-render robots directive, e.g. "index, follow".</summary>
    public string RobotsContent => $"{(NoIndex ? "noindex" : "index")}, {(NoFollow ? "nofollow" : "follow")}";
}
