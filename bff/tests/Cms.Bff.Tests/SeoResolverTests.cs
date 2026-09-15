using Cms.Bff.Contentful;
using Cms.Bff.Options;
using Cms.Bff.Seo;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cms.Bff.Tests;

public class SeoResolverTests
{
    private static readonly SiteOptions Site = new() { BaseUrl = "https://www.example.co.za" };

    private static SeoResolver Resolver() => new(Microsoft.Extensions.Options.Options.Create(Site));

    private static ResolvedEntry Seo(Dictionary<string, object?> fields) =>
        new("seo-1", "seo", fields, null);

    [Fact]
    public void Uses_the_page_seo_entry_when_present()
    {
        var seo = Resolver().Resolve(
            pageSeo: Seo(new() { ["metaTitle"] = "Tax-Free Investing", ["metaDescription"] = "Invest without paying tax." }),
            fallbackSeo: Seo(new() { ["metaTitle"] = "Fallback", ["metaDescription"] = "Fallback description." }),
            slug: "tax-free-investing",
            tokens: null);

        seo.MetaTitle.Should().Be("Tax-Free Investing");
        seo.MetaDescription.Should().Be("Invest without paying tax.");
    }

    [Fact]
    public void Falls_back_to_site_settings_when_the_page_has_no_seo_entry()
    {
        var seo = Resolver().Resolve(
            pageSeo: null,
            fallbackSeo: Seo(new() { ["metaTitle"] = "Own the Market", ["metaDescription"] = "Index investing made simple." }),
            slug: "about",
            tokens: null);

        seo.MetaTitle.Should().Be("Own the Market");
        seo.MetaDescription.Should().Be("Index investing made simple.");
    }

    [Fact]
    public void Computes_an_absolute_canonical_url_from_the_slug()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "how-to-invest", null);
        seo.CanonicalUrl.Should().Be("https://www.example.co.za/how-to-invest");
    }

    [Fact]
    public void Computes_the_homepage_canonical_without_a_trailing_path()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "", null);
        seo.CanonicalUrl.Should().Be("https://www.example.co.za/");
    }

    [Fact]
    public void An_authored_canonical_override_wins()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D", ["canonicalUrl"] = "https://www.example.co.za/funds" }),
            null, "funds/stx40", null);

        seo.CanonicalUrl.Should().Be("https://www.example.co.za/funds");
    }

    [Fact]
    public void Substitutes_live_data_tokens_into_authored_metadata()
    {
        var seo = Resolver().Resolve(
            Seo(new()
            {
                ["metaTitle"] = "{{fund.name}} | Fund Details",
                ["metaDescription"] = "{{fund.name}} trades at R{{fund.price}} today. Code {{fund.code}}.",
            }),
            null,
            "funds/stx40",
            new Dictionary<string, string>
            {
                ["fund.name"] = "Satrix 40 ETF",
                ["fund.price"] = "88.42",
                ["fund.code"] = "STX40",
            });

        seo.MetaTitle.Should().Be("Satrix 40 ETF | Fund Details");
        seo.MetaDescription.Should().Be("Satrix 40 ETF trades at R88.42 today. Code STX40.");
    }

    [Fact]
    public void Leaves_unmatched_tokens_out_rather_than_printing_braces_to_users()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "{{fund.name}} fund", ["metaDescription"] = "D" }), null, "funds/x", null);

        seo.MetaTitle.Should().Be("fund");
        seo.MetaTitle.Should().NotContain("{{");
    }

    [Fact]
    public void Maps_the_noindex_and_nofollow_toggles_to_a_robots_directive()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D", ["noindex"] = true, ["nofollow"] = true }),
            null, "campaign", null);

        seo.NoIndex.Should().BeTrue();
        seo.NoFollow.Should().BeTrue();
        seo.RobotsContent.Should().Be("noindex, nofollow");
    }

    [Fact]
    public void Defaults_to_indexable_and_followable()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "about", null);
        seo.RobotsContent.Should().Be("index, follow");
    }

    [Fact]
    public void Open_graph_falls_back_to_the_meta_title_and_description()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "Tax-Free", ["metaDescription"] = "No tax." }), null, "tfsa", null);

        seo.OgTitle.Should().Be("Tax-Free");
        seo.OgDescription.Should().Be("No tax.");
    }
}
