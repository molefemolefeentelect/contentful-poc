namespace Cms.Bff.FundData;

public interface IFundDataClient
{
    Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct);
    Task<FundDetail?> GetFundAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct);
    Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct);
}
