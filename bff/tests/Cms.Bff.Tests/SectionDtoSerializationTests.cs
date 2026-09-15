using System.Text.Json;
using Cms.Bff.Sections;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class SectionDtoSerializationTests
{
    private static List<SectionDto> AllSectionTypes() => new()
    {
        new HeroSection { Id = "hero1", Heading = "Own the market" },
        new RichTextSection { Id = "rt1" },
        new CardGridSection { Id = "cg1" },
        new CtaBannerSection { Id = "cta1", Heading = "Sign up", Cta = new LinkDto("Go", "/go") },
        new ArticleTeaserListSection { Id = "atl1" },
        new FundListSection
        {
            Id = "fl1",
            Funds = new[]
            {
                new FundSummaryDto("F1", "Growth Fund", "A growth fund", "equity", 12.34m, 0.5m, 1.2m, 8.9m),
            },
        },
        new FundDetailSection { Id = "fd1" },
        new FaqAccordionSection { Id = "faq1" },
        new ImageWithTextSection { Id = "iwt1" },
        new DisclaimerSection { Id = "disc1" },
    };

    private static readonly Dictionary<string, string> ExpectedDiscriminators = new()
    {
        ["hero1"] = "sectionHero",
        ["rt1"] = "sectionRichText",
        ["cg1"] = "sectionCardGrid",
        ["cta1"] = "sectionCtaBanner",
        ["atl1"] = "sectionArticleTeaserList",
        ["fl1"] = "sectionFundListWidget",
        ["fd1"] = "sectionFundDetailWidget",
        ["faq1"] = "sectionFaqAccordion",
        ["iwt1"] = "sectionImageWithText",
        ["disc1"] = "sectionDisclaimer",
    };

    [Fact]
    public void Serializes_a_mixed_list_of_all_section_types_without_throwing()
    {
        var sections = AllSectionTypes();

        var act = () => JsonSerializer.Serialize(sections);

        act.Should().NotThrow();
    }

    [Fact]
    public void Each_serialized_section_carries_the_correct_type_discriminator()
    {
        var json = JsonSerializer.Serialize(AllSectionTypes());
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var id = element.GetProperty("Id").GetString()!;
            var discriminator = element.GetProperty("__type").GetString();

            discriminator.Should().Be(ExpectedDiscriminators[id], $"entry '{id}' should carry its expected __type discriminator");
        }
    }

    [Fact]
    public void Output_never_contains_a_stray_capital_Type_key()
    {
        var json = JsonSerializer.Serialize(AllSectionTypes());
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            element.TryGetProperty("Type", out _).Should().BeFalse(
                "the [JsonIgnore] on each override should suppress the stray unrenamed 'Type' property");
        }
    }

    [Fact]
    public void Round_trips_legitimate_fields_alongside_the_discriminator()
    {
        var json = JsonSerializer.Serialize(AllSectionTypes());
        using var doc = JsonDocument.Parse(json);

        var hero = doc.RootElement.EnumerateArray().First(e => e.GetProperty("Id").GetString() == "hero1");
        hero.GetProperty("Heading").GetString().Should().Be("Own the market");

        var fundList = doc.RootElement.EnumerateArray().First(e => e.GetProperty("Id").GetString() == "fl1");
        var funds = fundList.GetProperty("Funds");
        funds.GetArrayLength().Should().Be(1);
        funds[0].GetProperty("Name").GetString().Should().Be("Growth Fund");
        funds[0].GetProperty("Code").GetString().Should().Be("F1");
    }

    [Fact]
    public void Deserializes_back_into_the_correct_concrete_type()
    {
        var original = new List<SectionDto> { new HeroSection { Id = "hero1", Heading = "Own the market" } };
        var json = JsonSerializer.Serialize(original);

        var deserialized = JsonSerializer.Deserialize<List<SectionDto>>(json);

        deserialized.Should().ContainSingle();
        deserialized![0].Should().BeOfType<HeroSection>()
            .Which.Heading.Should().Be("Own the market");
        deserialized[0].Type.Should().Be("sectionHero");
    }
}
