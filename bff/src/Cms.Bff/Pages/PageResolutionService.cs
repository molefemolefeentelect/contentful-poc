using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.FundData;
using Cms.Bff.Options;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Pages;

public sealed class PageResolutionService
{
    /// <summary>Reserved slug of the single Contentful template that serves every fund detail URL.</summary>
    public const string FundDetailTemplateSlug = "funds/_detail";

    private readonly IContentfulClient _contentful;
    private readonly EntryLinkResolver _links;
    private readonly SectionResolverRegistry _sections;
    private readonly SeoResolver _seo;
    private readonly JsonLdBuilderRegistry _jsonLd;
    private readonly IFundDataClient _funds;
    private readonly PageCache _cache;
    private readonly SiteOptions _site;
    private readonly ILogger<PageResolutionService> _logger;

    public PageResolutionService(
        IContentfulClient contentful,
        EntryLinkResolver links,
        SectionResolverRegistry sections,
        SeoResolver seo,
        JsonLdBuilderRegistry jsonLd,
        IFundDataClient funds,
        PageCache cache,
        IOptions<SiteOptions> site,
        ILogger<PageResolutionService> logger)
    {
        _contentful = contentful;
        _links = links;
        _sections = sections;
        _seo = seo;
        _jsonLd = jsonLd;
        _funds = funds;
        _cache = cache;
        _site = site.Value;
        _logger = logger;
    }

    public Task<PageResponse?> GetPageAsync(string slug, bool preview, string? fundCode, CancellationToken ct)
    {
        var cacheKey = $"page:{slug}|fund:{fundCode ?? "-"}";

        return _cache.GetOrCreateAsync<PageResponse?>(cacheKey, preview, async () =>
        {
            var page = await FetchPageEntryAsync(slug, preview, ct);
            if (page is null) return (null, Array.Empty<string>());

            var response = await BuildAsync(page, slug, preview, fundCode, ct);
            return (response, response.EntryIds);
        });
    }

    private async Task<ResolvedEntry?> FetchPageEntryAsync(string slug, bool preview, CancellationToken ct)
    {
        // Contentful's CDA silently drops query params with an empty value (fields.slug=),
        // so an exact-match filter can't be used to find the homepage, whose slug IS "".
        // Fetch all pages and match client-side instead, just for that one case.
        if (slug.Length == 0)
        {
            var all = await _contentful.QueryAsync(new ContentfulQuery("page", null, Include: 6, Limit: 100), preview, ct);
            return _links.ResolveItems(all).FirstOrDefault(e => e.GetString("slug") == "");
        }

        var result = await _contentful.QueryAsync(
            new ContentfulQuery("page", new Dictionary<string, string> { ["fields.slug"] = slug }, Include: 6, Limit: 1),
            preview, ct);

        return result.Items.Count == 0 ? null : _links.ResolveItems(result).FirstOrDefault();
    }

    private async Task<PageResponse> BuildAsync(ResolvedEntry page, string slug, bool preview, string? fundCode, CancellationToken ct)
    {
        var context = new SectionContext(slug, fundCode, preview);
        var sectionEntries = page.GetEntries("sections");
        var sections = await _sections.ResolveAllAsync(sectionEntries, context, ct);

        // Live-data tokens let an admin author "{{fund.name}} | Fund Details" once and
        // have it resolve per fund. Only populated when the page carries a fund widget.
        var tokens = BuildTokens(sections);

        var fallbackSeo = await GetDefaultSeoAsync(preview, ct);
        var seo = _seo.Resolve(page.GetEntry("seo"), fallbackSeo, EffectiveSlug(slug, fundCode), tokens);

        if (string.IsNullOrWhiteSpace(seo.MetaTitle))
        {
            var pageTitle = page.GetString("title") ?? "";
            _logger.LogWarning(
                "Page '{Slug}' (entry {EntryId}) has no SEO entry and no site-wide default SEO — falling back to the page title '{Title}' for metaTitle. Configure siteSettings.defaultSeo to avoid this.",
                slug, page.Id, pageTitle);
            seo = seo with { MetaTitle = pageTitle, OgTitle = string.IsNullOrWhiteSpace(seo.OgTitle) ? pageTitle : seo.OgTitle };
        }

        var breadcrumbs = await BuildBreadcrumbsAsync(page, seo, ct);

        var jsonLd = _jsonLd.Build(new JsonLdContext(seo, slug, sections, null, breadcrumbs));

        return new PageResponse
        {
            Id = page.Id,
            Slug = slug,
            Title = page.GetString("title") ?? "",
            PageType = page.GetString("pageType") ?? "marketing",
            Seo = seo,
            JsonLd = jsonLd,
            Breadcrumbs = breadcrumbs,
            Sections = sections,
            UpdatedAt = page.UpdatedAt,
            EntryIds = page.CollectEntryIds(),
        };
    }

    /// <summary>The fund detail template renders at /funds/{code}, not at its own reserved slug.</summary>
    private static string EffectiveSlug(string slug, string? fundCode) =>
        slug == FundDetailTemplateSlug && !string.IsNullOrWhiteSpace(fundCode)
            ? $"funds/{fundCode.ToLowerInvariant()}"
            : slug;

    private static IReadOnlyDictionary<string, string>? BuildTokens(IReadOnlyList<SectionDto> sections)
    {
        var fund = sections.OfType<FundDetailSection>().FirstOrDefault(f => !f.Unavailable);
        if (fund is null) return null;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fund.name"] = fund.Name ?? "",
            ["fund.code"] = fund.Code ?? "",
            ["fund.price"] = fund.Nav?.ToString("0.##") ?? "",
            ["fund.ter"] = fund.Ter?.ToString("0.##") ?? "",
        };
    }

    private const string SiteSettingsCacheKey = "site-settings:default-seo";

    private Task<ResolvedEntry?> GetDefaultSeoAsync(bool preview, CancellationToken ct) =>
        _cache.GetOrCreateAsync<ResolvedEntry?>(SiteSettingsCacheKey, preview, async () =>
        {
            var response = await _contentful.QueryAsync(new ContentfulQuery("siteSettings", Include: 3, Limit: 1), preview, ct);
            var settings = _links.ResolveItems(response).FirstOrDefault();
            var defaultSeo = settings?.GetEntry("defaultSeo");
            var entryIds = settings?.CollectEntryIds() ?? Array.Empty<string>();
            return (defaultSeo, entryIds);
        });

    private Task<IReadOnlyList<BreadcrumbDto>> BuildBreadcrumbsAsync(ResolvedEntry page, SeoDto seo, CancellationToken ct)
    {
        var crumbs = new List<BreadcrumbDto>();

        // Walk the authored parent chain, not the URL string: the admin user controls it.
        var current = page;
        var guard = 0;
        while (current is not null && guard++ < 6)
        {
            var slug = current.GetString("slug") ?? "";
            crumbs.Insert(0, new BreadcrumbDto(current.GetString("title") ?? "", _seo.BuildCanonical(slug)));
            current = current.GetEntry("breadcrumbParent");
        }

        if (crumbs.Count > 0) crumbs[^1] = crumbs[^1] with { Url = seo.CanonicalUrl };

        return Task.FromResult<IReadOnlyList<BreadcrumbDto>>(crumbs);
    }
}
