using Cms.Bff.Contentful;
using Cms.Bff.FundData;
using Cms.Bff.Sections;
using Cms.Bff.Sections.Resolvers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class FundWidgetResolverTests
{
    private static ResolvedEntry Entry(string id, string contentType, Dictionary<string, object?> fields) =>
        new(id, contentType, fields, null);

    private sealed class StubFundDataClient : IFundDataClient
    {
        public bool ShouldThrow { get; init; }
        public bool ShouldThrowOnPrices { get; init; }
        public string? LastRequestedCategoryId { get; private set; }
        public int? LastRequestedTop { get; private set; }

        public Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct)
        {
            if (ShouldThrow) throw new HttpRequestException("fund api down");
            LastRequestedCategoryId = categoryId;
            LastRequestedTop = top;
            return Task.FromResult<IReadOnlyList<FundSummary>>(new[]
            {
                new FundSummary("STX40", "Satrix 40 ETF", "Top 40 tracker", "1", 88.42m, 0.62m, 0.10m, 14.8m),
            });
        }

        public Task<FundDetail?> GetFundAsync(string code, CancellationToken ct)
        {
            if (ShouldThrow) throw new HttpRequestException("fund api down");
            return Task.FromResult(code == "STX40"
                ? new FundDetail("STX40", "ZAE000027108", "Satrix 40 ETF", "Top 40 tracker", "1",
                    0.10m, 88.42m, 0.62m, new DateOnly(2000, 11, 27), "https://example.invalid/f.pdf",
                    new FundPerformance(14.8m, 11.2m, 9.6m, 13.1m))
                : null);
        }

        public Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct)
        {
            if (ShouldThrowOnPrices) throw new HttpRequestException("fund price api down");
            return Task.FromResult<IReadOnlyList<FundPrice>>(new[] { new FundPrice(new DateOnly(2026, 9, 15), 88.42m) });
        }

        public Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FundCategory>>(new[] { new FundCategory("1", "Local Equity", "local-equity") });
    }

    [Fact]
    public async Task Fund_list_passes_contentful_configuration_to_the_data_api_and_merges_the_results()
    {
        var client = new StubFundDataClient();
        var entry = Entry("fl1", "sectionFundListWidget", new()
        {
            ["heading"] = "Our funds",
            ["categoryId"] = "1",
            ["topN"] = 3m,
            ["displayVariant"] = "table",
            ["showFilters"] = true,
        });

        var section = (FundListSection)(await new FundListSectionResolver(client, NullLogger<FundListSectionResolver>.Instance)
            .ResolveAsync(entry, SectionContext.ForPage("funds"), CancellationToken.None))!;

        client.LastRequestedCategoryId.Should().Be("1");
        client.LastRequestedTop.Should().Be(3);
        section.Heading.Should().Be("Our funds");
        section.DisplayVariant.Should().Be("table");
        section.Funds.Should().ContainSingle().Which.Name.Should().Be("Satrix 40 ETF");
        section.Categories.Should().ContainSingle();
    }

    [Fact]
    public async Task CategoryId_all_is_treated_as_no_filter()
    {
        var client = new StubFundDataClient();
        var entry = Entry("fl1", "sectionFundListWidget", new()
        {
            ["heading"] = "All Funds",
            ["categoryId"] = "all",
        });

        await new FundListSectionResolver(client, NullLogger<FundListSectionResolver>.Instance)
            .ResolveAsync(entry, SectionContext.ForPage("funds"), CancellationToken.None);

        client.LastRequestedCategoryId.Should().BeNull("\"all\" is a Contentful sentinel for \"no filter\", not a real category id");
    }

    [Fact]
    public async Task Fund_detail_uses_the_route_override_when_contentful_leaves_the_code_blank()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new()
        {
            ["fundCode"] = null,
            ["showPerformance"] = true,
            ["showPriceHistory"] = true,
        });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient(), NullLogger<FundDetailSectionResolver>.Instance)
            .ResolveAsync(entry, new SectionContext("funds/_detail", "STX40", false), CancellationToken.None))!;

        section.Code.Should().Be("STX40");
        section.Name.Should().Be("Satrix 40 ETF");
        section.Nav.Should().Be(88.42m);
        section.Performance!.OneYear.Should().Be(14.8m);
        section.Prices.Should().ContainSingle();
        section.Unavailable.Should().BeFalse();
    }

    [Fact]
    public async Task An_authored_fund_code_wins_over_the_route_override()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new() { ["fundCode"] = "STX40" });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient(), NullLogger<FundDetailSectionResolver>.Instance)
            .ResolveAsync(entry, new SectionContext("some-campaign", "STXWDM", false), CancellationToken.None))!;

        section.Code.Should().Be("STX40", "a bespoke single-fund page must not be hijacked by the route param");
    }

    [Fact]
    public async Task An_unknown_fund_degrades_the_section_rather_than_failing_the_page()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new() { ["fundCode"] = "NOPE" });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient(), NullLogger<FundDetailSectionResolver>.Instance)
            .ResolveAsync(entry, SectionContext.ForPage("funds/_detail"), CancellationToken.None))!;

        section.Unavailable.Should().BeTrue();
        section.Name.Should().BeNull();
    }

    [Fact]
    public async Task An_upstream_outage_degrades_the_fund_list_to_empty_rather_than_throwing()
    {
        var entry = Entry("fl1", "sectionFundListWidget", new() { ["heading"] = "Our funds" });

        var section = (FundListSection)(await new FundListSectionResolver(new StubFundDataClient { ShouldThrow = true }, NullLogger<FundListSectionResolver>.Instance)
            .ResolveAsync(entry, SectionContext.ForPage("funds"), CancellationToken.None))!;

        section.Heading.Should().Be("Our funds", "authored copy must still render when the data API is down");
        section.Funds.Should().BeEmpty();
    }

    [Fact]
    public async Task A_price_history_failure_degrades_only_the_price_chart_not_the_whole_fund()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new()
        {
            ["fundCode"] = "STX40",
            ["showPriceHistory"] = true,
        });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(
                new StubFundDataClient { ShouldThrowOnPrices = true }, NullLogger<FundDetailSectionResolver>.Instance)
            .ResolveAsync(entry, SectionContext.ForPage("funds/_detail"), CancellationToken.None))!;

        section.Unavailable.Should().BeFalse();
        section.Name.Should().Be("Satrix 40 ETF");
        section.Nav.Should().Be(88.42m);
        section.Prices.Should().BeEmpty();
    }
}
