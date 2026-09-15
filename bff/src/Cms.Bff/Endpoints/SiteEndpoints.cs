using Cms.Bff.Options;
using Cms.Bff.Pages;

namespace Cms.Bff.Endpoints;

public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/site-settings", async (SiteService site, bool? preview, CancellationToken ct) =>
        {
            var settings = await site.GetSiteSettingsAsync(preview ?? false, ct);
            return settings is null
                ? Results.NotFound(new { message = "No site settings configured." })
                : Results.Ok(settings);
        });

        app.MapGet("/api/navigation", async (SiteService site, bool? preview, CancellationToken ct) =>
        {
            var settings = await site.GetSiteSettingsAsync(preview ?? false, ct);
            return Results.Ok(settings?.Navigations ?? Array.Empty<NavigationDto>());
        });

        app.MapGet("/api/sitemap", async (SitemapService sitemap, CancellationToken ct) =>
            Results.Ok(await sitemap.GetEntriesAsync(ct)));

        app.MapGet("/health", (ContentfulModeInfo modeInfo) =>
            Results.Ok(new { status = "ok", mode = modeInfo.Mode }));
    }
}
