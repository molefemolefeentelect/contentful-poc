using Cms.Bff.Contentful;
using Cms.Bff.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class FixtureContentfulClientTests
{
    // Same content-root convention as the real app: when launched via
    // `dotnet run --project bff/src/Cms.Bff` from the repo root, env.ContentRootPath
    // is the Cms.Bff project directory, and ContentfulOptions.FixturePath's default
    // ("../../../contentful/seed/fixtures/entries.json") climbs back up to
    // <repo-root>/contentful/seed/fixtures/entries.json.
    private static FixtureContentfulClient CreateClient()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"));
        var contentRoot = Path.Combine(repoRoot, "bff", "src", "Cms.Bff");

        var options = Microsoft.Extensions.Options.Options.Create(new ContentfulOptions());
        return new FixtureContentfulClient(options, new EnvStub(contentRoot), NullLogger<FixtureContentfulClient>.Instance);
    }

    [Fact]
    public async Task Descending_order_returns_articles_newest_first()
    {
        var client = CreateClient();

        var result = await client.QueryAsync(new ContentfulQuery("article", Order: "-fields.publishDate"), preview: false);

        result.Items.Select(e => e.Sys.Id).Should().Equal(
            "article-fee-cuts-2026",
            "article-index-investing-101",
            "article-new-tfsa-fund-launch",
            "article-market-outlook-2026");
    }

    [Fact]
    public async Task Ascending_order_returns_articles_oldest_first()
    {
        var client = CreateClient();

        var result = await client.QueryAsync(new ContentfulQuery("article", Order: "fields.publishDate"), preview: false);

        result.Items.Select(e => e.Sys.Id).Should().Equal(
            "article-market-outlook-2026",
            "article-new-tfsa-fund-launch",
            "article-index-investing-101",
            "article-fee-cuts-2026");
    }

    [Fact]
    public async Task Unrecognized_filter_key_throws_instead_of_matching_everything()
    {
        var client = CreateClient();

        // Neither "sys.id" nor a "fields." prefix — an unsupported Contentful query
        // pattern (e.g. a "sys.updatedAt[lte]" range operator) that the fixture client
        // does not implement. It must fail loudly, not silently match every entry.
        var filters = new Dictionary<string, string> { ["sys.updatedAt[lte]"] = "2026-01-01" };
        var query = new ContentfulQuery("article", Filters: filters);

        var act = async () => await client.QueryAsync(query, preview: false);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Slug_filter_still_returns_exactly_the_matching_page()
    {
        var client = CreateClient();

        var filters = new Dictionary<string, string> { ["fields.slug"] = "about" };
        var query = new ContentfulQuery("page", Filters: filters);

        var result = await client.QueryAsync(query, preview: false);

        result.Items.Select(e => e.Sys.Id).Should().Equal("page-about");
    }

    [Fact]
    public async Task Empty_string_slug_filter_matches_only_the_homepage()
    {
        var client = CreateClient();

        // The homepage is authored with fields.slug = "" (it renders at "/"). This pins
        // Matches' string-equality check as an exact match against "", not a falsy/absent
        // check that would either match nothing or match every page missing a slug.
        var filters = new Dictionary<string, string> { ["fields.slug"] = "" };
        var query = new ContentfulQuery("page", Filters: filters);

        var result = await client.QueryAsync(query, preview: false);

        result.Items.Select(e => e.Sys.Id).Should().Equal("page-home");
    }

    private sealed class EnvStub : IWebHostEnvironment
    {
        public EnvStub(string contentRootPath) => ContentRootPath = contentRootPath;
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "Test";
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
