namespace Cms.Bff.Seo.JsonLd;

public sealed class WebPageJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "WebPage";

    public Dictionary<string, object?>? Build(JsonLdContext context) =>
        new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebPage",
            ["name"] = context.Seo.MetaTitle,
            ["description"] = context.Seo.MetaDescription,
            ["url"] = context.Seo.CanonicalUrl,
        };
}
