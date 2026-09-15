using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class FaqAccordionSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionFaqAccordion";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new FaqAccordionSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Items = entry.GetEntries("items")
                .Select(i => new FaqItemDto(
                    i.Id,
                    i.GetString("question") ?? "",
                    i.GetRichText("answer"),
                    RichTextFlattener.ToPlainText(i.GetRichText("answer"))))
                .ToList(),
            EmitFaqSchema = entry.GetBool("emitFaqSchema"),
        });
}
