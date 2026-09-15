using System.Text.Json;
using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.Options;
using Cms.Bff.Sections;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Pages;

public sealed class SiteService
{
    private readonly IContentfulClient _contentful;
    private readonly EntryLinkResolver _links;
    private readonly PageCache _cache;
    private readonly SiteOptions _site;

    public SiteService(IContentfulClient contentful, EntryLinkResolver links, PageCache cache, IOptions<SiteOptions> site)
    {
        _contentful = contentful;
        _links = links;
        _cache = cache;
        _site = site.Value;
    }

    public async Task<SiteSettingsResponse?> GetSiteSettingsAsync(bool preview, CancellationToken ct)
    {
        const string cacheKey = "site-settings:full";
        return await _cache.GetOrCreateAsync<SiteSettingsResponse?>(cacheKey, preview, async () =>
        {
            var settingsResult = await _contentful.QueryAsync(new ContentfulQuery("siteSettings", Include: 3, Limit: 1), preview, ct);
            var settings = _links.ResolveItems(settingsResult).FirstOrDefault();
            if (settings is null) return (null, Array.Empty<string>());

            var navResult = await _contentful.QueryAsync(new ContentfulQuery("navigation", Include: 6, Limit: 10), preview, ct);
            var navEntries = _links.ResolveItems(navResult);

            var response = new SiteSettingsResponse
            {
                SiteName = settings.GetString("siteName") ?? "",
                Logo = SectionMapping.ToImage(settings.GetEntry("logo")),
                DisclaimerText = settings.GetRichText("disclaimerText"),
                SocialLinks = ParseSocialLinks(settings.GetRichText("socialLinks")),
                Navigations = navEntries.Select(MapNavigation).ToList(),
                JsonLd = BuildSiteJsonLd(settings),
            };

            var entryIds = settings.CollectEntryIds()
                .Concat(navEntries.SelectMany(n => n.CollectEntryIds()))
                .Distinct()
                .ToList();

            return (response, entryIds);
        });
    }

    private static IReadOnlyList<SocialLinkDto> ParseSocialLinks(JsonElement? socialLinks)
    {
        if (socialLinks is not { ValueKind: JsonValueKind.Object } obj) return Array.Empty<SocialLinkDto>();

        return obj.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.String)
            .Select(p => new SocialLinkDto(Capitalize(p.Name), p.Value.GetString() ?? ""))
            .ToList();
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static NavigationDto MapNavigation(ResolvedEntry nav) => new(
        nav.GetString("key") ?? "",
        nav.GetEntries("items").Where(i => !i.IsStub).Select(MapNavigationItem).ToList());

    private static NavigationItemDto MapNavigationItem(ResolvedEntry item)
    {
        var label = item.GetString("label") ?? "";
        var linked = item.GetEntry("page");
        var children = item.GetEntries("children").Where(c => !c.IsStub).Select(MapNavigationItem).ToList();

        if (linked is not null)
        {
            var url = linked.ContentTypeId == "article"
                ? $"/news/{linked.GetString("slug")}"
                : $"/{linked.GetString("slug")}";
            return new NavigationItemDto(label, url, External: false, children);
        }

        var externalUrl = item.GetString("externalUrl");
        return new NavigationItemDto(label, externalUrl ?? "", External: externalUrl is not null, children);
    }

    private IReadOnlyList<Dictionary<string, object?>> BuildSiteJsonLd(ResolvedEntry settings)
    {
        var siteName = settings.GetString("siteName") ?? "";
        var baseUrl = _site.BaseUrl.TrimEnd('/');

        return new List<Dictionary<string, object?>>
        {
            new()
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Organization",
                ["name"] = settings.GetString("organizationLegalName") ?? siteName,
                ["url"] = baseUrl + "/",
                ["logo"] = SectionMapping.ToImage(settings.GetEntry("logo"))?.Url,
                ["sameAs"] = settings.GetStrings("organizationSameAs"),
            },
            new()
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "WebSite",
                ["name"] = siteName,
                ["url"] = baseUrl + "/",
            },
        };
    }
}
