using Cms.FundData.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<FundStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "fund-data-api" }));

app.MapGet("/categories", (FundStore store) => Results.Ok(store.GetCategories()));

app.MapGet("/funds", (FundStore store, string? categoryId, int? top) =>
    Results.Ok(store.GetFunds(categoryId, top)));

app.MapGet("/funds/{codeOrIsin}", (FundStore store, string codeOrIsin) =>
{
    var fund = store.GetFund(codeOrIsin);
    return fund is null ? Results.NotFound(new { message = $"No fund with code or ISIN '{codeOrIsin}'." }) : Results.Ok(fund);
});

app.MapGet("/funds/{code}/prices", (FundStore store, string code, int? days) =>
{
    var window = Math.Clamp(days ?? 90, 1, 365);
    var prices = store.GetPrices(code, window);
    return prices.Count == 0
        ? Results.NotFound(new { message = $"No fund with code '{code}'." })
        : Results.Ok(prices);
});

app.Run();
