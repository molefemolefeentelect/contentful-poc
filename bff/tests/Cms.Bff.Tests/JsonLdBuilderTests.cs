using System.Text.Json;
using Cms.Bff.Contentful;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class JsonLdBuilderTests
{
    private static SeoDto Seo(string type) => new()
    {
        MetaTitle = "Satrix 40 ETF | Fund Details",
        MetaDescription = "Track the Top 40.",
        OgTitle = "Satrix 40 ETF",
        OgDescription = "Track the Top 40.",
        CanonicalUrl = "https://www.example.co.za/funds/stx40",
        StructuredDataType = type,
    };

    private static JsonLdBuilderRegistry Registry(params IJsonLdBuilder[] builders) =>
        new(builders, NullLogger<JsonLdBuilderRegistry>.Instance);

    [Fact]
    public void Product_schema_is_built_from_live_fund_data_not_hand_coded()
    {
        var fund = new FundDetailSection
        {
            Id = "fd1", Code = "STX40", Name = "Satrix 40 ETF", Isin = "ZAE000027108",
            ShortDescription = "Top 40 tracker", Nav = 88.42m, Ter = 0.10m,
        };

        var context = new JsonLdContext(Seo("Product"), "funds/stx40", new SectionDto[] { fund }, null, Array.Empty<BreadcrumbDto>());
        var result = Registry(new ProductJsonLdBuilder(NullLogger<ProductJsonLdBuilder>.Instance)).Build(context).Single();
        var json = JsonSerializer.SerializeToElement(result);

        json.GetProperty("@type").GetString().Should().Be("Product");
        json.GetProperty("name").GetString().Should().Be("Satrix 40 ETF");
        json.GetProperty("offers").GetProperty("price").GetString().Should().Be("88.42");
        json.GetProperty("offers").GetProperty("priceCurrency").GetString().Should().Be("ZAR");
    }

    [Fact]
    public void Faq_schema_is_emitted_only_when_the_author_opted_in()
    {
        var optedIn = new FaqAccordionSection
        {
            Id = "faq1", EmitFaqSchema = true,
            Items = new[] { new FaqItemDto("q1", "What is a TFSA?", null, "A tax-free savings account.") },
        };
        var optedOut = optedIn with { Id = "faq2", EmitFaqSchema = false };

        var builder = new FaqPageJsonLdBuilder();

        var withSchema = Registry(builder).Build(
            new JsonLdContext(Seo("FAQPage"), "tfsa", new SectionDto[] { optedIn }, null, Array.Empty<BreadcrumbDto>()));
        var withoutSchema = Registry(builder).Build(
            new JsonLdContext(Seo("FAQPage"), "tfsa", new SectionDto[] { optedOut }, null, Array.Empty<BreadcrumbDto>()));

        var json = JsonSerializer.SerializeToElement(withSchema.Single());
        json.GetProperty("@type").GetString().Should().Be("FAQPage");
        json.GetProperty("mainEntity")[0].GetProperty("acceptedAnswer").GetProperty("text").GetString()
            .Should().Be("A tax-free savings account.");

        withoutSchema.Should().BeEmpty();
    }

    [Fact]
    public void Breadcrumbs_are_emitted_when_the_page_has_an_authored_parent()
    {
        var crumbs = new[]
        {
            new BreadcrumbDto("Home", "https://www.example.co.za/"),
            new BreadcrumbDto("Funds", "https://www.example.co.za/funds"),
            new BreadcrumbDto("Satrix 40 ETF", "https://www.example.co.za/funds/stx40"),
        };

        var result = Registry(new BreadcrumbJsonLdBuilder()).Build(
            new JsonLdContext(Seo("Product"), "funds/stx40", Array.Empty<SectionDto>(), null, crumbs)).Single();

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("@type").GetString().Should().Be("BreadcrumbList");
        json.GetProperty("itemListElement").GetArrayLength().Should().Be(3);
        json.GetProperty("itemListElement")[1].GetProperty("position").GetInt32().Should().Be(2);
    }

    [Fact]
    public void An_unknown_structured_data_type_produces_no_schema_and_does_not_throw()
    {
        var context = new JsonLdContext(Seo("SomethingNobodyImplemented"), "x", Array.Empty<SectionDto>(), null, Array.Empty<BreadcrumbDto>());
        Registry(new ProductJsonLdBuilder(NullLogger<ProductJsonLdBuilder>.Instance)).Build(context).Should().BeEmpty();
    }

    [Fact]
    public void Structured_data_overrides_are_merged_over_generated_properties()
    {
        var seo = Seo("Product") with
        {
            StructuredDataOverrides = JsonDocument.Parse("""{ "brand": "Satrix", "name": "Overridden name" }""").RootElement,
        };
        var fund = new FundDetailSection { Id = "fd1", Code = "STX40", Name = "Satrix 40 ETF", Nav = 88.42m };

        var result = Registry(new ProductJsonLdBuilder(NullLogger<ProductJsonLdBuilder>.Instance)).Build(
            new JsonLdContext(seo, "funds/stx40", new SectionDto[] { fund }, null, Array.Empty<BreadcrumbDto>())).Single();

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("name").GetString().Should().Be("Overridden name");
        json.GetProperty("brand").GetString().Should().Be("Satrix");
    }

    [Fact]
    public void Multiple_fund_detail_widgets_log_a_warning_and_build_from_the_first_one_only()
    {
        var first = new FundDetailSection { Id = "fd1", Code = "STX40", Name = "Satrix 40 ETF", Nav = 88.42m };
        var second = new FundDetailSection { Id = "fd2", Code = "STXWDM", Name = "Satrix MSCI World ETF", Nav = 55.10m };

        var context = new JsonLdContext(
            Seo("Product"), "funds/stx40", new SectionDto[] { first, second }, null, Array.Empty<BreadcrumbDto>());

        var result = Registry(new ProductJsonLdBuilder(NullLogger<ProductJsonLdBuilder>.Instance)).Build(context).Single();
        var json = JsonSerializer.SerializeToElement(result);

        json.GetProperty("@type").GetString().Should().Be("Product");
        json.GetProperty("name").GetString().Should().Be("Satrix 40 ETF");
        json.GetProperty("sku").GetString().Should().Be("STX40");
    }

    [Fact]
    public void Article_schema_omits_keys_for_missing_optional_fields_instead_of_emitting_null()
    {
        var article = new ResolvedEntry(
            "art1",
            "article",
            new Dictionary<string, object?>
            {
                ["title"] = "TFSA Basics",
                ["excerpt"] = "What you need to know.",
                ["publishDate"] = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            },
            null);

        var context = new JsonLdContext(Seo("Article"), "articles/tfsa-basics", Array.Empty<SectionDto>(), article, Array.Empty<BreadcrumbDto>());

        var result = Registry(new ArticleJsonLdBuilder()).Build(context).Single();

        result.Should().ContainKey("headline");
        result.Should().ContainKey("datePublished");
        result.Should().ContainKey("dateModified");
        result.Should().NotContainKey("image");
        result.Should().NotContainKey("author");

        var json = JsonSerializer.SerializeToElement(result);
        json.TryGetProperty("image", out _).Should().BeFalse();
        json.TryGetProperty("author", out _).Should().BeFalse();
    }
}
