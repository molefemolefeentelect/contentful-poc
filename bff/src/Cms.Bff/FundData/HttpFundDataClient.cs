using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cms.Bff.FundData;

public sealed class HttpFundDataClient : IFundDataClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public HttpFundDataClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(categoryId)) query.Add($"categoryId={Uri.EscapeDataString(categoryId)}");
        if (top is > 0) query.Add($"top={top}");
        var url = "/funds" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        // The Fund Data API nests one-year return under "performance.oneYear" (same shape as
        // /funds/{code}), not as a flat "oneYearReturn" — deserializing straight into
        // FundSummary would silently leave OneYearReturn at 0 for every fund.
        var wire = await _http.GetFromJsonAsync<List<FundSummaryWire>>(url, Json, ct) ?? new List<FundSummaryWire>();
        return wire.Select(w => new FundSummary(
            w.Code, w.Name, w.ShortDescription, w.CategoryId, w.Nav, w.DayChangePercent, w.Ter, w.Performance.OneYear)).ToList();
    }

    private sealed record FundSummaryWire(
        string Code, string Name, string ShortDescription, string CategoryId,
        decimal Nav, decimal DayChangePercent, decimal Ter, FundPerformance Performance);

    public async Task<FundDetail?> GetFundAsync(string code, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/funds/{Uri.EscapeDataString(code)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FundDetail>(Json, ct);
    }

    public async Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/funds/{Uri.EscapeDataString(code)}/prices?days={days}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return Array.Empty<FundPrice>();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FundPrice>>(Json, ct) ?? new List<FundPrice>();
    }

    public async Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct) =>
        await _http.GetFromJsonAsync<List<FundCategory>>("/categories", Json, ct) ?? new List<FundCategory>();
}
