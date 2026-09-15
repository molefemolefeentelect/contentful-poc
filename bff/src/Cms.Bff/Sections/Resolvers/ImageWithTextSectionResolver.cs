using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class ImageWithTextSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionImageWithText";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new ImageWithTextSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Body = entry.GetRichText("body"),
            Image = SectionMapping.ToImage(entry.GetEntry("image")),
            ImagePosition = entry.GetString("imagePosition") ?? "left",
            Cta = SectionMapping.ToLink(entry.GetString("ctaLabel"), entry.GetString("ctaUrl")),
        });
}
