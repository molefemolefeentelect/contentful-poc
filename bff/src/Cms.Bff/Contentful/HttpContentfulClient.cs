using System.Net.Http.Json;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Contentful;

public sealed class HttpContentfulClient : IContentfulClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ContentfulOptions _options;
    private readonly ILogger<HttpContentfulClient> _logger;

    public HttpContentfulClient(IHttpClientFactory factory, IOptions<ContentfulOptions> options, ILogger<HttpContentfulClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CdaResponse> QueryAsync(ContentfulQuery query, bool preview, CancellationToken ct = default)
    {
        var host = preview ? "preview.contentful.com" : "cdn.contentful.com";
        var token = preview ? _options.PreviewToken : _options.DeliveryToken;

        var parameters = new List<string>
        {
            $"content_type={Uri.EscapeDataString(query.ContentType)}",
            $"include={query.Include}",
            $"limit={query.Limit}",
        };
        if (query.Order is not null) parameters.Add($"order={Uri.EscapeDataString(query.Order)}");
        foreach (var (k, v) in query.Filters ?? new Dictionary<string, string>())
            parameters.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");

        var url = $"https://{host}/spaces/{_options.SpaceId}/environments/{_options.Environment}/entries?{string.Join("&", parameters)}";

        var client = _factory.CreateClient(nameof(HttpContentfulClient));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Contentful returned {Status} for {ContentType}: {Body}", (int)response.StatusCode, query.ContentType, body);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<CdaResponse>(cancellationToken: ct)
               ?? new CdaResponse();
    }
}
