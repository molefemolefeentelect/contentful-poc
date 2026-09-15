using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Endpoints;

public static class PreviewEndpoints
{
    public static void MapPreviewEndpoints(this IEndpointRouteBuilder app)
    {
        // Token broker for Next.js draft mode. Next owns the cookie because cookies are
        // per-origin; this endpoint only confirms the handshake secret is valid and
        // says which URL should be shown.
        app.MapGet("/api/preview", (
            string? secret,
            string? slug,
            IOptions<IntegrationOptions> integration,
            IOptions<SiteOptions> site) =>
        {
            if (secret != integration.Value.PreviewSecret)
                return Results.Unauthorized();

            var path = (slug ?? "").Trim('/');
            return Results.Ok(new
            {
                valid = true,
                redirectTo = path.Length == 0 ? "/" : "/" + path,
                siteUrl = site.Value.BaseUrl.TrimEnd('/'),
            });
        });
    }
}
