namespace Cms.Bff.Seo.JsonLd;

public sealed class OrganizationJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "Organization";

    // NOTE: This builder only has access to JsonLdContext, which carries no
    // siteSettings data (no base URL, no sameAs links). It exists to complete
    // the registry for a page whose structuredDataType is (perhaps mistakenly)
    // set to "Organization" directly, producing a minimal-but-valid schema
    // rather than nothing. The REAL sitewide Organization + WebSite JSON-LD
    // (with the actual site base URL and sameAs social links) is built
    // directly by SiteService from siteSettings data, not through this class.
    public Dictionary<string, object?>? Build(JsonLdContext context) =>
        new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = context.Seo.OgTitle,
            ["url"] = context.Seo.CanonicalUrl,
        };
}
