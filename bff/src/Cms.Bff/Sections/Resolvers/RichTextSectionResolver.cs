using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class RichTextSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionRichText";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new RichTextSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Body = entry.GetRichText("body"),
            Width = entry.GetString("width") ?? "default",
        });
}
