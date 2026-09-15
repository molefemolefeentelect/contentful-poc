namespace Cms.FundData.Api.Models;

public record FundCategory(string Id, string Name, string Slug);

public record Performance(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public record PricePoint(DateOnly Date, decimal Nav);

public record Fund(
    string Code,
    string Isin,
    string Name,
    string CategoryId,
    string ShortDescription,
    decimal Ter,
    decimal Nav,
    decimal DayChangePercent,
    DateOnly InceptionDate,
    string FactsheetUrl,
    Performance Performance);
