using System.Globalization;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed class ProductJsonLdBuilder : IJsonLdBuilder
{
    private readonly ILogger<ProductJsonLdBuilder> _logger;

    public ProductJsonLdBuilder(ILogger<ProductJsonLdBuilder> logger) => _logger = logger;

    public string StructuredDataType => "Product";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        var available = context.Sections.OfType<FundDetailSection>().Where(f => !f.Unavailable).ToList();
        if (available.Count == 0) return null;

        if (available.Count > 1)
        {
            _logger.LogWarning(
                "Page '{Slug}' has {Count} fund detail widgets; Product schema is built from only the first ({Code}) and the rest are not represented in structured data.",
                context.Slug, available.Count, available[0].Code);
        }

        var fund = available[0];

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = fund.Name,
            ["description"] = fund.ShortDescription,
            ["url"] = context.Seo.CanonicalUrl,
            ["sku"] = fund.Code,
            ["category"] = "Exchange Traded Fund",
        };

        if (!string.IsNullOrWhiteSpace(fund.Isin))
            schema["identifier"] = new Dictionary<string, object?>
            {
                ["@type"] = "PropertyValue", ["propertyID"] = "ISIN", ["value"] = fund.Isin,
            };

        if (fund.Nav is { } nav)
            schema["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = nav.ToString("0.##", CultureInfo.InvariantCulture),
                ["priceCurrency"] = "ZAR",
                ["availability"] = "https://schema.org/InStock",
                ["url"] = context.Seo.CanonicalUrl,
            };

        return schema;
    }
}
