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

            // Browsers normalize backslashes to forward slashes for http(s) URLs, so a
            // slug like "\evil.com" would otherwise survive Trim('/') untouched and turn
            // "/\evil.com" into a scheme-relative redirect to https://evil.com. Collapse
            // backslashes to slashes first so the trim also catches them.
            var path = (slug ?? "").Replace('\\', '/').Trim('/');
            return Results.Ok(new
            {
                valid = true,
                redirectTo = path.Length == 0 ? "/" : "/" + path,
                siteUrl = site.Value.BaseUrl.TrimEnd('/'),
            });
        });
    }
}
