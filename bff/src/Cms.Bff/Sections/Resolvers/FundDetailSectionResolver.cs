using Cms.Bff.Contentful;
using Cms.Bff.FundData;

namespace Cms.Bff.Sections.Resolvers;

public sealed class FundDetailSectionResolver : ISectionResolver
{
    private const int PriceWindowDays = 90;
    private readonly IFundDataClient _funds;
    private readonly ILogger<FundDetailSectionResolver> _logger;

    public FundDetailSectionResolver(IFundDataClient funds, ILogger<FundDetailSectionResolver> logger)
    {
        _funds = funds;
        _logger = logger;
    }

    public string ContentTypeId => "sectionFundDetailWidget";

    public async Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        // An explicitly authored fundCode always wins so a bespoke single-fund landing
        // page keeps working; the route override only fills a deliberately blank field
        // on the shared funds/_detail template.
        var code = entry.GetString("fundCode");
        if (string.IsNullOrWhiteSpace(code)) code = context.FundCodeOverride;

        if (string.IsNullOrWhiteSpace(code))
            return new FundDetailSection { Id = entry.Id, Unavailable = true };

        FundDetail? fund;
        try
        {
            fund = await _funds.GetFundAsync(code, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fund Data API call failed while fetching fund '{Code}' for entry {EntryId} on page '{Slug}'. Fund detail unavailable.",
                code, entry.Id, context.Slug);
            fund = null;
        }

        // Price history is an optional enhancement: a failure here must not discard an
        // already-successful fund fetch (name, NAV, ISIN, factsheet, performance).
        IReadOnlyList<FundPrice> prices = Array.Empty<FundPrice>();
        if (fund is not null && entry.GetBool("showPriceHistory"))
        {
            try
            {
                prices = await _funds.GetPricesAsync(code, PriceWindowDays, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Fund Data API price history call failed for fund '{Code}' on page '{Slug}'. Price chart degraded to empty.",
                    code, context.Slug);
            }
        }

        if (fund is null)
            return new FundDetailSection { Id = entry.Id, Code = code, Unavailable = true };

        return new FundDetailSection
        {
            Id = entry.Id,
            Code = fund.Code,
            Name = fund.Name,
            Isin = fund.Isin,
            ShortDescription = fund.ShortDescription,
            Nav = fund.Nav,
            DayChangePercent = fund.DayChangePercent,
            Ter = fund.Ter,
            InceptionDate = fund.InceptionDate,
            FactsheetUrl = entry.GetBool("showFactsheet") ? fund.FactsheetUrl : null,
            Performance = entry.GetBool("showPerformance")
                ? new FundPerformanceDto(fund.Performance.OneYear, fund.Performance.ThreeYear, fund.Performance.FiveYear, fund.Performance.SinceInception)
                : null,
            Prices = prices.Select(p => new PricePointDto(p.Date, p.Nav)).ToList(),
            Unavailable = false,
        };
    }
}
