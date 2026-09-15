using System.Text.Json;
using Cms.FundData.Api.Models;

namespace Cms.FundData.Api.Data;

public sealed class FundStore
{
    private readonly List<Fund> _funds;
    private readonly List<FundCategory> _categories;

    public FundStore(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "funds.json");
        using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seed = JsonSerializer.Deserialize<SeedFile>(stream, options)
                   ?? throw new InvalidOperationException($"Could not read fund seed data at {path}");
        _funds = seed.Funds;
        _categories = seed.Categories;
    }

    public IReadOnlyList<FundCategory> GetCategories() => _categories;

    public IReadOnlyList<Fund> GetFunds(string? categoryId, int? top)
    {
        IEnumerable<Fund> query = _funds;
        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(f => f.CategoryId == categoryId);

        query = query.OrderByDescending(f => f.Performance.OneYear);

        if (top is > 0)
            query = query.Take(top.Value);

        return query.ToList();
    }

    public Fund? GetFund(string codeOrIsin) =>
        _funds.FirstOrDefault(f =>
            string.Equals(f.Code, codeOrIsin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Isin, codeOrIsin, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PricePoint> GetPrices(string code, int days)
    {
        var fund = GetFund(code);
        if (fund is null) return Array.Empty<PricePoint>();

        var rng = new Random(fund.Code.GetHashCode(StringComparison.Ordinal));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var points = new List<PricePoint>(days);
        var nav = fund.Nav;

        for (var i = days - 1; i >= 0; i--)
        {
            var drift = (decimal)((rng.NextDouble() - 0.49) * 0.012);
            nav = Math.Round(nav * (1 + drift), 2);
            points.Add(new PricePoint(today.AddDays(-i), nav));
        }

        return points;
    }

    private sealed record SeedFile(List<FundCategory> Categories, List<Fund> Funds);
}
