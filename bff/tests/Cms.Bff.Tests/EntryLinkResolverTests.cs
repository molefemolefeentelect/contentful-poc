using System.Text.Json;
using Cms.Bff.Contentful;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class EntryLinkResolverTests
{
    private static CdaResponse Parse(string json) =>
        JsonSerializer.Deserialize<CdaResponse>(json)!;

    private const string NestedJson = """
    {
      "items": [{
        "sys": { "id": "page-1", "type": "Entry", "contentType": { "sys": { "id": "page", "type": "Link", "linkType": "ContentType" } } },
        "fields": {
          "title": "Home",
          "sections": [
            { "sys": { "id": "hero-1", "type": "Link", "linkType": "Entry" } },
            { "sys": { "id": "missing-1", "type": "Link", "linkType": "Entry" } }
          ]
        }
      }],
      "includes": {
        "Entry": [{
          "sys": { "id": "hero-1", "type": "Entry", "contentType": { "sys": { "id": "sectionHero", "type": "Link", "linkType": "ContentType" } } },
          "fields": { "heading": "Own the market", "image": { "sys": { "id": "asset-1", "type": "Link", "linkType": "Asset" } } }
        }],
        "Asset": [{
          "sys": { "id": "asset-1", "type": "Asset" },
          "fields": { "title": "Hero", "file": { "url": "//images.ctfassets.net/x/hero.jpg", "details": { "image": { "width": 1920, "height": 960 } } } }
        }]
      }
    }
    """;

    [Fact]
    public void Resolves_nested_entry_links_in_order()
    {
        var page = new EntryLinkResolver().Resolve(Parse(NestedJson), "page-1")!;

        page.ContentTypeId.Should().Be("page");
        page.GetString("title").Should().Be("Home");

        var sections = page.GetEntries("sections");
        sections.Should().HaveCount(1, "the dangling link must be dropped, not rendered as a hole");
        sections[0].ContentTypeId.Should().Be("sectionHero");
        sections[0].GetString("heading").Should().Be("Own the market");
    }

    [Fact]
    public void Resolves_asset_links_to_absolute_urls()
    {
        var page = new EntryLinkResolver().Resolve(Parse(NestedJson), "page-1")!;
        var asset = page.GetEntries("sections")[0].GetAsset("image")!;

        asset.Url.Should().Be("https://images.ctfassets.net/x/hero.jpg");
        asset.Width.Should().Be(1920);
        asset.Height.Should().Be(960);
    }

    [Fact]
    public void Returns_null_when_the_requested_entry_is_absent()
    {
        new EntryLinkResolver().Resolve(Parse(NestedJson), "nope").Should().BeNull();
    }

    [Fact]
    public void Terminates_on_circular_references()
    {
        const string circular = """
        {
          "items": [{
            "sys": { "id": "a", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "title": "A", "related": [{ "sys": { "id": "b", "type": "Link", "linkType": "Entry" } }] }
          }],
          "includes": {
            "Entry": [{
              "sys": { "id": "b", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
              "fields": { "title": "B", "related": [{ "sys": { "id": "a", "type": "Link", "linkType": "Entry" } }] }
            }]
          }
        }
        """;

        var a = new EntryLinkResolver().Resolve(Parse(circular), "a")!;
        var b = a.GetEntries("related").Single();
        var backToA = b.GetEntries("related").Single();

        b.GetString("title").Should().Be("B");
        backToA.Id.Should().Be("a");
        backToA.GetEntries("related").Should().BeEmpty("the cycle must be cut, not followed");
    }

    [Fact]
    public void Asset_with_explicit_null_file_degrades_to_null_instead_of_throwing()
    {
        const string json = """
        {
          "items": [{
            "sys": { "id": "img-holder", "type": "Entry", "contentType": { "sys": { "id": "mediaImage", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "image": { "sys": { "id": "asset-null", "type": "Link", "linkType": "Asset" } } }
          }],
          "includes": {
            "Asset": [{
              "sys": { "id": "asset-null", "type": "Asset" },
              "fields": { "title": "Still processing", "file": null }
            }]
          }
        }
        """;

        // An asset still processing, or missing a value for the requested locale, must
        // degrade to a null image — not throw and crash the whole page resolution.
        var holder = new EntryLinkResolver().Resolve(Parse(json), "img-holder")!;

        holder.GetAsset("image").Should().BeNull();
    }

    [Fact]
    public void GetInt_returns_null_instead_of_throwing_for_numbers_outside_int32_range()
    {
        const string json = """
        {
          "items": [{
            "sys": { "id": "e1", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "hugeNumber": 5000000000 }
          }]
        }
        """;

        var entry = new EntryLinkResolver().Resolve(Parse(json), "e1")!;

        // Must degrade to null, not throw OverflowException, since GetInt is a
        // general-purpose helper used on arbitrary numeric fields.
        entry.GetInt("hugeNumber").Should().BeNull();
    }

    [Fact]
    public void IsStub_marks_cycle_cut_entries_but_not_normally_resolved_ones()
    {
        const string circular = """
        {
          "items": [{
            "sys": { "id": "a", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "title": "A", "related": [{ "sys": { "id": "b", "type": "Link", "linkType": "Entry" } }] }
          }],
          "includes": {
            "Entry": [{
              "sys": { "id": "b", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
              "fields": { "title": "B", "related": [{ "sys": { "id": "a", "type": "Link", "linkType": "Entry" } }] }
            }]
          }
        }
        """;

        var a = new EntryLinkResolver().Resolve(Parse(circular), "a")!;
        var b = a.GetEntries("related").Single();
        var backToA = b.GetEntries("related").Single();

        backToA.IsStub.Should().BeTrue("it was cut off by cycle detection, not genuinely empty");

        var page = new EntryLinkResolver().Resolve(Parse(NestedJson), "page-1")!;
        page.IsStub.Should().BeFalse("a normally-resolved entry must not be mistaken for a stub");
    }

    [Fact]
    public void Asset_with_explicit_null_width_or_height_degrades_to_null_dimensions_instead_of_throwing()
    {
        const string json = """
        {
          "items": [{
            "sys": { "id": "img-holder", "type": "Entry", "contentType": { "sys": { "id": "mediaImage", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "image": { "sys": { "id": "asset-partial", "type": "Link", "linkType": "Asset" } } }
          }],
          "includes": {
            "Asset": [{
              "sys": { "id": "asset-partial", "type": "Asset" },
              "fields": { "title": "Partially processed", "file": { "url": "//images.ctfassets.net/x/partial.jpg", "details": { "image": { "width": null, "height": null } } } }
            }]
          }
        }
        """;

        // Same "still processing" scenario as the null-file case, but for the nested
        // width/height fields specifically — must degrade, not throw InvalidOperationException.
        var holder = new EntryLinkResolver().Resolve(Parse(json), "img-holder")!;
        var asset = holder.GetAsset("image")!;

        asset.Should().NotBeNull();
        asset.Url.Should().Be("https://images.ctfassets.net/x/partial.jpg");
        asset.Width.Should().BeNull();
        asset.Height.Should().BeNull();
    }
}
