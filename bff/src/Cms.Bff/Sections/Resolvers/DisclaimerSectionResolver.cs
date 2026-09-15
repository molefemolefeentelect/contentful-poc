using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class DisclaimerSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionDisclaimer";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new DisclaimerSection
        {
            Id = entry.Id,
            Label = entry.GetString("label"),
            Body = entry.GetRichText("body"),
            Severity = entry.GetString("severity") ?? "info",
            Collapsible = entry.GetBool("collapsible"),
        });
}
