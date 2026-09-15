using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class HeroSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionHero";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new HeroSection
        {
            Id = entry.Id,
            Eyebrow = entry.GetString("eyebrow"),
            Heading = entry.GetString("heading") ?? "",
            Subheading = entry.GetString("subheading"),
            BackgroundImage = SectionMapping.ToImage(entry.GetEntry("backgroundImage")),
            PrimaryCta = SectionMapping.ToLink(entry.GetString("primaryCtaLabel"), entry.GetString("primaryCtaUrl")),
            SecondaryCta = SectionMapping.ToLink(entry.GetString("secondaryCtaLabel"), entry.GetString("secondaryCtaUrl")),
            Variant = entry.GetString("variant") ?? "default",
        });
}
