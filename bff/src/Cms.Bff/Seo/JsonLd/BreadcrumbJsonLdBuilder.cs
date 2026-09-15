namespace Cms.Bff.Seo.JsonLd;

public sealed class BreadcrumbJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "BreadcrumbList";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        if (context.Breadcrumbs.Count < 2) return null;

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = context.Breadcrumbs.Select((crumb, index) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = crumb.Name,
                ["item"] = crumb.Url,
            }).ToList(),
        };
    }
}
