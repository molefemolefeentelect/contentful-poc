using Cms.Bff.Contentful;
using Cms.Bff.FundData;

namespace Cms.Bff.Sections.Resolvers;

public sealed class FundListSectionResolver : ISectionResolver
{
    private readonly IFundDataClient _funds;
    private readonly ILogger<FundListSectionResolver> _logger;

    public FundListSectionResolver(IFundDataClient funds, ILogger<FundListSectionResolver> logger)
    {
        _funds = funds;
        _logger = logger;
    }

    public string ContentTypeId => "sectionFundListWidget";

    public async Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        var categoryId = entry.GetString("categoryId");
        var topN = entry.GetInt("topN");
        var showFilters = entry.GetBool("showFilters");

        IReadOnlyList<FundSummary> funds = Array.Empty<FundSummary>();
        IReadOnlyList<FundCategory> categories = Array.Empty<FundCategory>();

        try
        {
            var fundsTask = _funds.GetFundsAsync(categoryId, topN, ct);
            var categoriesTask = showFilters ? _funds.GetCategoriesAsync(ct) : Task.FromResult<IReadOnlyList<FundCategory>>(Array.Empty<FundCategory>());
            await Task.WhenAll(fundsTask, categoriesTask);
            funds = fundsTask.Result;
            categories = categoriesTask.Result;
        }
        catch (Exception ex)
        {
            // Authored copy still renders; the data block degrades to empty.
            // This resolver swallows the exception itself (it never reaches
            // SectionResolverRegistry's catch), so it must log here or an outage
            // would otherwise leave zero trace anywhere.
            _logger.LogError(ex,
                "Fund Data API call failed while resolving sectionFundListWidget entry {EntryId} on page '{Slug}'. Fund list degraded to empty.",
                entry.Id, context.Slug);
        }

        return new FundListSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Intro = entry.GetString("intro"),
            CategoryId = categoryId,
            ShowFilters = showFilters,
            DisplayVariant = entry.GetString("displayVariant") ?? "grid",
            Categories = categories.Select(c => new FundCategoryDto(c.Id, c.Name, c.Slug)).ToList(),
            Funds = funds.Select(f => new FundSummaryDto(
                f.Code, f.Name, f.ShortDescription, f.CategoryId, f.Nav, f.DayChangePercent, f.Ter, f.OneYearReturn)).ToList(),
        };
    }
}
