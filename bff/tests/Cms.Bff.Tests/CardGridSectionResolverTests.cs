using Cms.Bff.Contentful;
using Cms.Bff.Sections;
using Cms.Bff.Sections.Resolvers;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class CardGridSectionResolverTests
{
    [Fact]
    public async Task Drops_stub_linked_cards_instead_of_rendering_them_blank()
    {
        var goodCard = new ResolvedEntry(
            "card-good",
            "card",
            new Dictionary<string, object?> { ["title"] = "Real card" },
            null);

        var stubCard = new ResolvedEntry(
            "card-stub",
            "card",
            new Dictionary<string, object?>(),
            null)
        {
            IsStub = true,
        };

        var entry = new ResolvedEntry(
            "grid1",
            "sectionCardGrid",
            new Dictionary<string, object?>
            {
                ["heading"] = "Our products",
                ["cards"] = new List<object?> { goodCard, stubCard },
            },
            null);

        var result = await new CardGridSectionResolver().ResolveAsync(entry, SectionContext.ForPage("/"), CancellationToken.None);

        result.Should().BeOfType<CardGridSection>();
        var section = (CardGridSection)result!;
        section.Cards.Should().ContainSingle();
        section.Cards[0].Id.Should().Be("card-good");
        section.Cards[0].Title.Should().Be("Real card");
    }
}
