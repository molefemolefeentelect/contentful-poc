using Cms.Bff.Contentful;

namespace Cms.Bff.Seo;

public interface ISeoResolver
{
    /// <param name="tokens">
    /// Live values substituted into authored metadata, e.g. "fund.name" -> "Satrix 40 ETF".
    /// Null for pages with no live data.
    /// </param>
    SeoDto Resolve(ResolvedEntry? pageSeo, ResolvedEntry? fallbackSeo, string slug, IReadOnlyDictionary<string, string>? tokens);
}
