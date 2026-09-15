namespace Cms.Bff.FundData;

public record FundCategory(string Id, string Name, string Slug);

public record FundPerformance(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public record FundPrice(DateOnly Date, decimal Nav);

public record FundSummary(
    string Code, string Name, string ShortDescription, string CategoryId,
    decimal Nav, decimal DayChangePercent, decimal Ter, decimal OneYearReturn);

public record FundDetail(
    string Code, string Isin, string Name, string ShortDescription, string CategoryId,
    decimal Ter, decimal Nav, decimal DayChangePercent, DateOnly InceptionDate,
    string FactsheetUrl, FundPerformance Performance);
