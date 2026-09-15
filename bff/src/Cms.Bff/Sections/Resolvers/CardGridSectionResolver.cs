using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class CardGridSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionCardGrid";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new CardGridSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Intro = entry.GetString("intro"),
            Cards = entry.GetEntries("cards")
                .Where(c => !c.IsStub)
                .Select(c => new CardDto(c.Id, c.GetString("title") ?? "", c.GetString("body"), SectionMapping.ToImage(c.GetEntry("image")), SectionMapping.ToLink(c.GetString("linkLabel"), c.GetString("linkUrl")), c.GetString("icon")))
                .ToList(),
            Columns = entry.GetInt("columns") ?? 3,
        });
}
