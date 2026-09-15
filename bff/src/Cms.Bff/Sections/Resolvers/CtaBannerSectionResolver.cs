using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class CtaBannerSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionCtaBanner";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        var cta = SectionMapping.ToLink(entry.GetString("ctaLabel"), entry.GetString("ctaUrl"));
        if (cta is null) return Task.FromResult<SectionDto?>(null);

        return Task.FromResult<SectionDto?>(new CtaBannerSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading") ?? "",
            Body = entry.GetString("body"),
            Cta = cta,
            Variant = entry.GetString("variant") ?? "primary",
        });
    }
}
