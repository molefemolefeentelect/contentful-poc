using Cms.Bff.Contentful;

namespace Cms.Bff.Sections;

/// <param name="Slug">The page being rendered, used for logging and canonical URLs.</param>
/// <param name="FundCodeOverride">
/// Set only for the fund detail template route. Injected into a
/// sectionFundDetailWidget whose own fundCode is blank, so one Contentful template
/// serves every fund. An explicitly authored fundCode always wins.
/// </param>
public sealed record SectionContext(string Slug, string? FundCodeOverride, bool Preview)
{
    public static SectionContext ForPage(string slug) => new(slug, null, false);
}

public interface ISectionResolver
{
    string ContentTypeId { get; }
    Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct);
}
