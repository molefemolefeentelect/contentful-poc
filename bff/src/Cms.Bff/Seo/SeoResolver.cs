using System.Text.RegularExpressions;
using Cms.Bff.Contentful;
using Cms.Bff.Options;
using Cms.Bff.Sections;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Seo;

public sealed partial class SeoResolver : ISeoResolver
{
    private readonly SiteOptions _site;

    public SeoResolver(IOptions<SiteOptions> site) => _site = site.Value;

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}")]
    private static partial Regex TokenPattern();

    public SeoDto Resolve(ResolvedEntry? pageSeo, ResolvedEntry? fallbackSeo, string slug, IReadOnlyDictionary<string, string>? tokens)
    {
        var source = pageSeo ?? fallbackSeo;

        var metaTitle = Substitute(source?.GetString("metaTitle") ?? "", tokens);
        var metaDescription = Substitute(source?.GetString("metaDescription") ?? "", tokens);

        var ogTitle = Substitute(source?.GetString("ogTitle"), tokens);
        var ogDescription = Substitute(source?.GetString("ogDescription"), tokens);

        var canonicalOverride = source?.GetString("canonicalUrl");

        return new SeoDto
        {
            MetaTitle = metaTitle,
            MetaDescription = metaDescription,
            OgTitle = string.IsNullOrWhiteSpace(ogTitle) ? metaTitle : ogTitle,
            OgDescription = string.IsNullOrWhiteSpace(ogDescription) ? metaDescription : ogDescription,
            OgImage = SectionMapping.ToImage(source?.GetEntry("ogImage")),
            CanonicalUrl = string.IsNullOrWhiteSpace(canonicalOverride) ? BuildCanonical(slug) : canonicalOverride,
            NoIndex = source?.GetBool("noindex") ?? false,
            NoFollow = source?.GetBool("nofollow") ?? false,
            StructuredDataType = source?.GetString("structuredDataType") ?? "WebPage",
            StructuredDataOverrides = source?.GetRichText("structuredDataOverrides"),
        };
    }

    public string BuildCanonical(string slug)
    {
        var baseUrl = _site.BaseUrl.TrimEnd('/');
        var path = slug.Trim('/');
        return path.Length == 0 ? baseUrl + "/" : $"{baseUrl}/{path}";
    }

    /// <summary>
    /// Replaces {{token}} placeholders with live values. Unmatched tokens are removed
    /// rather than left in place, so a missing value never renders literal braces into
    /// a search result.
    /// </summary>
    private static string Substitute(string? template, IReadOnlyDictionary<string, string>? tokens)
    {
        if (string.IsNullOrEmpty(template)) return template ?? "";
        if (!template.Contains("{{", StringComparison.Ordinal)) return template;

        var replaced = TokenPattern().Replace(template, match =>
            tokens is not null && tokens.TryGetValue(match.Groups[1].Value, out var value) ? value : "");

        return string.Join(" ", replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
