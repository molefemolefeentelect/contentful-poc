using Cms.Bff.Pages;

namespace Cms.Bff.Endpoints;

public static class PageEndpoints
{
    public static void MapPageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pages/{*slug}", async (
            PageResolutionService pages,
            string? slug,
            bool? preview,
            string? fundCode,
            CancellationToken ct) =>
        {
            var normalised = (slug ?? "").Trim('/');
            var page = await pages.GetPageAsync(normalised, preview ?? false, fundCode, ct);

            return page is null
                ? Results.NotFound(new { message = $"No published page at '/{normalised}'." })
                : Results.Ok(page);
        });

        // Convenience alias so the homepage does not need an empty catch-all segment.
        app.MapGet("/api/pages", async (PageResolutionService pages, bool? preview, CancellationToken ct) =>
        {
            var page = await pages.GetPageAsync("", preview ?? false, null, ct);
            return page is null ? Results.NotFound(new { message = "No published homepage." }) : Results.Ok(page);
        });
    }
}
