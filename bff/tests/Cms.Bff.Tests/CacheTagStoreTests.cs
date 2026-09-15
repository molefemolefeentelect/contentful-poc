using Cms.Bff.Caching;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class CacheTagStoreTests
{
    [Fact]
    public void Evicts_every_page_that_embeds_the_changed_entry()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "sec-hero", "sec-global-disclaimer" });
        store.Associate("page:about", new[] { "page-about", "sec-global-disclaimer" });
        store.Associate("page:news", new[] { "page-news", "sec-teasers" });

        var affected = store.KeysFor("sec-global-disclaimer");

        affected.Should().BeEquivalentTo("page:/", "page:about");
    }

    [Fact]
    public void Returns_nothing_for_an_entry_no_cached_page_uses()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home" });

        store.KeysFor("some-unrelated-entry").Should().BeEmpty();
    }

    [Fact]
    public void Re_associating_a_key_replaces_its_previous_entry_ids()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "sec-old-cta" });
        store.Associate("page:/", new[] { "page-home", "sec-new-cta" });

        store.KeysFor("sec-old-cta").Should().BeEmpty("a removed section must stop invalidating the page");
        store.KeysFor("sec-new-cta").Should().ContainSingle().Which.Should().Be("page:/");
    }

    [Fact]
    public void Forgetting_a_key_removes_it_from_every_entry_index()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "shared" });
        store.Associate("page:about", new[] { "page-about", "shared" });

        store.Forget("page:/");

        store.KeysFor("shared").Should().ContainSingle().Which.Should().Be("page:about");
        store.KeysFor("page-home").Should().BeEmpty();
    }
}
