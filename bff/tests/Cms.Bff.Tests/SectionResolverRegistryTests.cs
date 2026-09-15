using Cms.Bff.Contentful;
using Cms.Bff.Sections;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class SectionResolverRegistryTests
{
    private static ResolvedEntry Entry(string id, string contentType, Dictionary<string, object?>? fields = null) =>
        new(id, contentType, fields ?? new Dictionary<string, object?>(), null);

    private static SectionResolverRegistry Registry(params ISectionResolver[] resolvers) =>
        new(resolvers, NullLogger<SectionResolverRegistry>.Instance);

    private sealed class FakeResolver : ISectionResolver
    {
        public string ContentTypeId => "sectionHero";
        public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
            Task.FromResult<SectionDto?>(new HeroSection { Id = entry.Id, Heading = entry.GetString("heading") ?? "" });
    }

    [Fact]
    public async Task Dispatches_to_the_resolver_registered_for_the_content_type()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[] { Entry("h1", "sectionHero", new() { ["heading"] = "Own the market" }) },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Should().ContainSingle();
        sections[0].Type.Should().Be("sectionHero");
        sections[0].Should().BeOfType<HeroSection>()
            .Which.Heading.Should().Be("Own the market");
    }

    [Fact]
    public async Task Drops_unknown_section_types_instead_of_throwing()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[]
            {
                Entry("h1", "sectionHero", new() { ["heading"] = "Kept" }),
                Entry("x1", "sectionFutureThing"),
            },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Should().ContainSingle("an unregistered section type must not break the page");
        sections[0].Id.Should().Be("h1");
    }

    [Fact]
    public async Task Preserves_author_ordering()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[]
            {
                Entry("a", "sectionHero", new() { ["heading"] = "first" }),
                Entry("b", "sectionHero", new() { ["heading"] = "second" }),
                Entry("c", "sectionHero", new() { ["heading"] = "third" }),
            },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Select(s => s.Id).Should().ContainInOrder("a", "b", "c");
    }
}
