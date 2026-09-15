using Cms.Bff.Contentful;
using Cms.Bff.FundData;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Pages;

public sealed class SitemapService
{
    private readonly IContentfulClient _contentful;
    private readonly EntryLinkResolver _links;
    private readonly IFundDataClient _funds;
    private readonly SiteOptions _site;

    public SitemapService(IContentfulClient contentful, EntryLinkResolver links, IFundDataClient funds, IOptions<SiteOptions> site)
    {
        _contentful = contentful;
        _links = links;
        _funds = funds;
        _site = site.Value;
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken ct)
    {
        var baseUrl = _site.BaseUrl.TrimEnd('/');
        var entries = new List<SitemapEntry>();

        var pagesResult = await _contentful.QueryAsync(new ContentfulQuery("page", Include: 0, Limit: 100), preview: false, ct);
        foreach (var page in _links.ResolveItems(pagesResult))
        {
            var slug = page.GetString("slug") ?? "";
            if (!IsPublicSlug(slug)) continue;

            var loc = slug.Length == 0 ? baseUrl + "/" : $"{baseUrl}/{slug}";
            var priority = slug.Length == 0 ? 1.0 : 0.8;
            entries.Add(new SitemapEntry(loc, page.UpdatedAt, "weekly", priority));
        }

        var articlesResult = await _contentful.QueryAsync(new ContentfulQuery("article", Include: 0, Limit: 100), preview: false, ct);
        foreach (var article in _links.ResolveItems(articlesResult))
        {
            var slug = article.GetString("slug") ?? "";
            var lastMod = article.GetDate("updatedDate") ?? article.GetDate("publishDate");
            entries.Add(new SitemapEntry($"{baseUrl}/news/{slug}", lastMod, "monthly", 0.6));
        }

        var funds = await _funds.GetFundsAsync(null, null, ct);
        foreach (var fund in funds)
            entries.Add(new SitemapEntry($"{baseUrl}/funds/{fund.Code}", null, "daily", 0.7));

        return entries;
    }

    private static bool IsPublicSlug(string slug) =>
        !slug.StartsWith('_') && !slug.Contains("/_", StringComparison.Ordinal);
}
