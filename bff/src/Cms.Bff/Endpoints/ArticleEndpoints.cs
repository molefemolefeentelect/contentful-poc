using Cms.Bff.Pages;

namespace Cms.Bff.Endpoints;

public static class ArticleEndpoints
{
    public static void MapArticleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/articles", async (
            ArticleService articles,
            string? category,
            int? page,
            int? pageSize,
            bool? preview,
            CancellationToken ct) =>
        {
            var (items, total) = await articles.GetArticlesAsync(
                category, page ?? 1, pageSize ?? 12, preview ?? false, ct);

            return Results.Ok(new { items, total, page = page ?? 1, pageSize = pageSize ?? 12 });
        });

        app.MapGet("/api/articles/{slug}", async (ArticleService articles, string slug, bool? preview, CancellationToken ct) =>
        {
            var article = await articles.GetArticleAsync(slug, preview ?? false, ct);
            return article is null
                ? Results.NotFound(new { message = $"No published article at '/{slug}'." })
                : Results.Ok(article);
        });
    }
}
