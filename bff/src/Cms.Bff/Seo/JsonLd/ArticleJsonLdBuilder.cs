using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed class ArticleJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "Article";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        if (context.Article is null) return null;

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = context.Article.GetString("title"),
            ["description"] = context.Article.GetString("excerpt"),
            ["mainEntityOfPage"] = context.Seo.CanonicalUrl,
        };

        var imageUrl = SectionMapping.ToImage(context.Article.GetEntry("featuredImage"))?.Url;
        if (imageUrl is not null) schema["image"] = imageUrl;

        // KNOWN LIMITATION: EntryLinkResolver's date-parsing heuristic (Task 3.2) can attach
        // a different UTC offset to the same authored date depending on the server's local
        // timezone; the "O" format below stays valid ISO 8601 either way, but the offset is
        // not guaranteed deterministic across environments. Not fixed here — tracked separately.
        var publishDate = context.Article.GetDate("publishDate");
        if (publishDate is not null) schema["datePublished"] = publishDate.Value.ToString("O");

        var modifiedDate = context.Article.GetDate("updatedDate") ?? publishDate;
        if (modifiedDate is not null) schema["dateModified"] = modifiedDate.Value.ToString("O");

        var authorName = context.Article.GetEntry("author")?.GetString("name");
        if (authorName is not null)
            schema["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = authorName };

        return schema;
    }
}
