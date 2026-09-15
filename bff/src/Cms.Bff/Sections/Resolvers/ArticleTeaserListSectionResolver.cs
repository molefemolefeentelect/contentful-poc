using Cms.Bff.Contentful;
using Cms.Bff.Pages;

namespace Cms.Bff.Sections.Resolvers;

public sealed class ArticleTeaserListSectionResolver : ISectionResolver
{
    private readonly ArticleService _articles;

    public ArticleTeaserListSectionResolver(ArticleService articles) => _articles = articles;

    public string ContentTypeId => "sectionArticleTeaserList";

    public async Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        var mode = entry.GetString("mode") ?? "latest";
        IReadOnlyList<ArticleTeaserDto> teasers;

        if (mode == "manual")
        {
            teasers = entry.GetEntries("articles")
                .Where(a => !a.IsStub)
                .Select(ArticleService.ToTeaser)
                .ToList();
        }
        else
        {
            var categorySlug = entry.GetEntry("category")?.GetString("slug");
            var limit = entry.GetInt("limit") ?? 6;
            var (items, _) = await _articles.GetArticlesAsync(categorySlug, page: 1, pageSize: limit, context.Preview, ct);
            teasers = items;
        }

        return new ArticleTeaserListSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Articles = teasers,
        };
    }
}
