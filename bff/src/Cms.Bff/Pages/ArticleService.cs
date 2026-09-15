using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

namespace Cms.Bff.Pages;

public sealed class ArticleService
{
    private readonly IContentfulClient _contentful;
    private readonly EntryLinkResolver _links;
    private readonly SeoResolver _seo;
    private readonly JsonLdBuilderRegistry _jsonLd;
    private readonly PageCache _cache;
    private readonly ILogger<ArticleService> _logger;

    public ArticleService(
        IContentfulClient contentful,
        EntryLinkResolver links,
        SeoResolver seo,
        JsonLdBuilderRegistry jsonLd,
        PageCache cache,
        ILogger<ArticleService> logger)
    {
        _contentful = contentful;
        _links = links;
        _seo = seo;
        _jsonLd = jsonLd;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ArticleResponse?> GetArticleAsync(string slug, bool preview, CancellationToken ct)
    {
        var cacheKey = $"article:{slug}";
        return await _cache.GetOrCreateAsync<ArticleResponse?>(cacheKey, preview, async () =>
        {
            var result = await _contentful.QueryAsync(
                new ContentfulQuery("article", new Dictionary<string, string> { ["fields.slug"] = slug }, Include: 4, Limit: 1),
                preview, ct);
            var article = result.Items.Count == 0 ? null : _links.ResolveItems(result).FirstOrDefault();
            if (article is null) return (null, Array.Empty<string>());

            var response = await BuildArticleResponseAsync(article, slug, preview, ct);
            return (response, response.EntryIds);
        });
    }

    public async Task<(IReadOnlyList<ArticleTeaserDto> Items, int Total)> GetArticlesAsync(
        string? categorySlug, int page, int pageSize, bool preview, CancellationToken ct)
    {
        var cacheKey = $"articles:{categorySlug ?? "-"}:{page}:{pageSize}";
        return await _cache.GetOrCreateAsync<(IReadOnlyList<ArticleTeaserDto>, int)>(cacheKey, preview, async () =>
        {
            var query = new ContentfulQuery("article", null, Include: 3, Limit: 100, Order: "-fields.publishDate");
            var result = await _contentful.QueryAsync(query, preview, ct);

            if (result.Items.Count >= query.Limit)
            {
                _logger.LogWarning(
                    "Article query hit its {Limit}-item cap; category filtering for '{CategorySlug}' happened over only those {Limit} most recent sitewide articles and totals/pagination may under-report. Add server-side category filtering and pagination (a Skip parameter on ContentfulQuery) before this becomes a real limitation.",
                    query.Limit, categorySlug ?? "(none)", query.Limit);
            }

            var all = _links.ResolveItems(result);

            if (!string.IsNullOrWhiteSpace(categorySlug))
                all = all.Where(a => a.GetEntry("category")?.GetString("slug") == categorySlug).ToList();

            var teasers = all.Select(ToTeaser).ToList();
            var pageItems = teasers.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var entryIds = all.SelectMany(a => a.CollectEntryIds()).Distinct().ToList();
            return ((pageItems, teasers.Count), entryIds);
        });
    }

    internal static ArticleTeaserDto ToTeaser(ResolvedEntry article) => new(
        article.Id,
        article.GetString("title") ?? "",
        article.GetString("slug") ?? "",
        article.GetString("excerpt"),
        SectionMapping.ToImage(article.GetEntry("featuredImage")),
        article.GetEntry("category")?.GetString("name"),
        article.GetDate("publishDate"),
        article.GetEntry("author")?.GetString("name"));

    private async Task<ArticleResponse> BuildArticleResponseAsync(ResolvedEntry article, string slug, bool preview, CancellationToken ct)
    {
        var fallbackSeo = await GetDefaultSeoAsync(preview, ct);
        var seo = _seo.Resolve(article.GetEntry("seo"), fallbackSeo, $"news/{slug}", null);

        if (string.IsNullOrWhiteSpace(seo.MetaDescription))
        {
            var summary = RichTextFlattener.Summarise(article.GetRichText("body"), 160);
            _logger.LogWarning(
                "Article '{Slug}' (entry {EntryId}) has no meta description on its SEO entry — falling back to a summary of the article body. Configure the article's SEO entry to avoid this.",
                slug, article.Id);
            seo = seo with { MetaDescription = summary, OgDescription = summary };
        }

        var jsonLd = _jsonLd.Build(new JsonLdContext(seo, $"news/{slug}", Array.Empty<SectionDto>(), article, Array.Empty<BreadcrumbDto>()));

        return new ArticleResponse
        {
            Id = article.Id,
            Slug = slug,
            Title = article.GetString("title") ?? "",
            Excerpt = article.GetString("excerpt"),
            Body = article.GetRichText("body"),
            FeaturedImage = SectionMapping.ToImage(article.GetEntry("featuredImage")),
            AuthorName = article.GetEntry("author")?.GetString("name"),
            AuthorJobTitle = article.GetEntry("author")?.GetString("jobTitle"),
            CategoryName = article.GetEntry("category")?.GetString("name"),
            CategorySlug = article.GetEntry("category")?.GetString("slug"),
            Tags = article.GetStrings("tags"),
            PublishDate = article.GetDate("publishDate"),
            UpdatedDate = article.GetDate("updatedDate"),
            Seo = seo,
            JsonLd = jsonLd,
            Related = article.GetEntries("relatedArticles").Where(a => !a.IsStub).Select(ToTeaser).ToList(),
            EntryIds = article.CollectEntryIds(),
        };
    }

    private const string SiteSettingsCacheKey = "site-settings:default-seo:article";

    private Task<ResolvedEntry?> GetDefaultSeoAsync(bool preview, CancellationToken ct) =>
        _cache.GetOrCreateAsync<ResolvedEntry?>(SiteSettingsCacheKey, preview, async () =>
        {
            var response = await _contentful.QueryAsync(new ContentfulQuery("siteSettings", Include: 3, Limit: 1), preview, ct);
            var settings = _links.ResolveItems(response).FirstOrDefault();
            var defaultSeo = settings?.GetEntry("defaultSeo");
            var entryIds = settings?.CollectEntryIds() ?? Array.Empty<string>();
            return (defaultSeo, entryIds);
        });
}
