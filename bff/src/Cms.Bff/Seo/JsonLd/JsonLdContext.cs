using Cms.Bff.Contentful;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed record BreadcrumbDto(string Name, string Url);

public sealed record JsonLdContext(
    SeoDto Seo,
    string Slug,
    IReadOnlyList<SectionDto> Sections,
    ResolvedEntry? Article,
    IReadOnlyList<BreadcrumbDto> Breadcrumbs);
