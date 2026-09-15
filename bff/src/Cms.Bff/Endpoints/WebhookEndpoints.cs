using System.Net.Http.Json;
using System.Text.Json;
using Cms.Bff.Caching;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/contentful", async (
            HttpRequest request,
            PageCache cache,
            IHttpClientFactory httpFactory,
            IOptions<IntegrationOptions> integration,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var expected = integration.Value.WebhookSecret;
            if (!request.Headers.TryGetValue("X-Webhook-Secret", out var provided) || provided != expected)
            {
                logger.LogWarning("Rejected Contentful webhook with missing or incorrect secret.");
                return Results.Unauthorized();
            }

            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            var root = document.RootElement;

            var entryId = root.TryGetProperty("sys", out var sys) && sys.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(entryId))
                return Results.BadRequest(new { message = "Webhook payload had no sys.id." });

            var evicted = cache.InvalidateEntry(entryId);

            // Tell Next.js to drop its own cached render of the same pages.
            var client = httpFactory.CreateClient("revalidate");
            try
            {
                using var response = await client.PostAsJsonAsync(
                    integration.Value.RevalidateUrl,
                    new { secret = integration.Value.PreviewSecret, entryId, tags = evicted },
                    ct);

                logger.LogInformation(
                    "Revalidation for entry {EntryId} returned {Status} ({Count} cache key(s) evicted).",
                    entryId, (int)response.StatusCode, evicted.Count);
            }
            catch (Exception ex)
            {
                // The BFF cache is already clear; the site self-heals on the next ISR pass.
                logger.LogError(ex, "Could not reach Next.js revalidate endpoint for entry {EntryId}.", entryId);
            }

            return Results.Ok(new { entryId, evicted });
        });
    }
}
