using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.Endpoints;
using Cms.Bff.FundData;
using Cms.Bff.Options;
using Cms.Bff.Pages;
using Cms.Bff.Sections;
using Cms.Bff.Sections.Resolvers;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ContentfulOptions>(builder.Configuration.GetSection(ContentfulOptions.Section));
builder.Services.Configure<FundDataOptions>(builder.Configuration.GetSection(FundDataOptions.Section));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));
builder.Services.Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.Section));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<CacheTagStore>();
builder.Services.AddSingleton<PageCache>();
builder.Services.AddSingleton<EntryLinkResolver>();

// Fixture mode needs no Contentful account, so the PoC runs before tokens exist.
var contentfulMode = builder.Configuration["Contentful:Mode"] ?? "Fixture";
if (string.Equals(contentfulMode, "Live", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IContentfulClient, HttpContentfulClient>();
else
    builder.Services.AddSingleton<IContentfulClient, FixtureContentfulClient>();
builder.Services.AddSingleton(new ContentfulModeInfo(contentfulMode));

builder.Services.AddHttpClient<IFundDataClient, HttpFundDataClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FundDataOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<ISectionResolver, HeroSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, RichTextSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, CardGridSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, CtaBannerSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, ImageWithTextSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, DisclaimerSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, FaqAccordionSectionResolver>();
builder.Services.AddScoped<ISectionResolver, ArticleTeaserListSectionResolver>();
builder.Services.AddScoped<ISectionResolver, FundListSectionResolver>();
builder.Services.AddScoped<ISectionResolver, FundDetailSectionResolver>();
builder.Services.AddScoped<SectionResolverRegistry>();

builder.Services.AddSingleton<SeoResolver>();
builder.Services.AddSingleton<ISeoResolver>(sp => sp.GetRequiredService<SeoResolver>());
builder.Services.AddSingleton<IJsonLdBuilder, WebPageJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, ArticleJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, ProductJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, FaqPageJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, BreadcrumbJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, OrganizationJsonLdBuilder>();
builder.Services.AddSingleton<JsonLdBuilderRegistry>();

builder.Services.AddScoped<PageResolutionService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<SitemapService>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["Site:BaseUrl"] ?? "http://localhost:3000")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.MapPageEndpoints();
app.MapArticleEndpoints();
app.MapSiteEndpoints();
app.MapWebhookEndpoints();
app.MapPreviewEndpoints();

app.Run();
