using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cms.Bff.Sections;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "__type")]
[JsonDerivedType(typeof(HeroSection), "sectionHero")]
[JsonDerivedType(typeof(RichTextSection), "sectionRichText")]
[JsonDerivedType(typeof(CardGridSection), "sectionCardGrid")]
[JsonDerivedType(typeof(CtaBannerSection), "sectionCtaBanner")]
[JsonDerivedType(typeof(ArticleTeaserListSection), "sectionArticleTeaserList")]
[JsonDerivedType(typeof(FundListSection), "sectionFundListWidget")]
[JsonDerivedType(typeof(FundDetailSection), "sectionFundDetailWidget")]
[JsonDerivedType(typeof(FaqAccordionSection), "sectionFaqAccordion")]
[JsonDerivedType(typeof(ImageWithTextSection), "sectionImageWithText")]
[JsonDerivedType(typeof(DisclaimerSection), "sectionDisclaimer")]
public abstract record SectionDto
{
    [JsonIgnore]
    public abstract string Type { get; }

    public required string Id { get; init; }
}

public sealed record ImageDto(string Url, string AltText, int? Width, int? Height, string? Caption);

public sealed record LinkDto(string Label, string Url);

public sealed record HeroSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionHero";
    public string? Eyebrow { get; init; }
    public required string Heading { get; init; }
    public string? Subheading { get; init; }
    public ImageDto? BackgroundImage { get; init; }
    public LinkDto? PrimaryCta { get; init; }
    public LinkDto? SecondaryCta { get; init; }
    public string Variant { get; init; } = "default";
}

public sealed record RichTextSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionRichText";
    public string? Heading { get; init; }
    public JsonElement? Body { get; init; }
    public string Width { get; init; } = "default";
}

public sealed record CardDto(string Id, string Title, string? Body, ImageDto? Image, LinkDto? Link, string? Icon);

public sealed record CardGridSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionCardGrid";
    public string? Heading { get; init; }
    public string? Intro { get; init; }
    public IReadOnlyList<CardDto> Cards { get; init; } = Array.Empty<CardDto>();
    public int Columns { get; init; } = 3;
}

public sealed record CtaBannerSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionCtaBanner";
    public required string Heading { get; init; }
    public string? Body { get; init; }
    public required LinkDto Cta { get; init; }
    public string Variant { get; init; } = "primary";
}

public sealed record ArticleTeaserDto(string Id, string Title, string Slug, string? Excerpt, ImageDto? Image, string? CategoryName, DateTimeOffset? PublishDate, string? AuthorName);

public sealed record ArticleTeaserListSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionArticleTeaserList";
    public string? Heading { get; init; }
    public IReadOnlyList<ArticleTeaserDto> Articles { get; init; } = Array.Empty<ArticleTeaserDto>();
}

public sealed record FundSummaryDto(string Code, string Name, string ShortDescription, string CategoryId, decimal Nav, decimal DayChangePercent, decimal Ter, decimal OneYearReturn);

public sealed record FundListSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionFundListWidget";
    public string? Heading { get; init; }
    public string? Intro { get; init; }
    public string? CategoryId { get; init; }
    public bool ShowFilters { get; init; }
    public string DisplayVariant { get; init; } = "grid";
    public IReadOnlyList<FundCategoryDto> Categories { get; init; } = Array.Empty<FundCategoryDto>();
    public IReadOnlyList<FundSummaryDto> Funds { get; init; } = Array.Empty<FundSummaryDto>();
}

public sealed record FundCategoryDto(string Id, string Name, string Slug);

public sealed record PricePointDto(DateOnly Date, decimal Nav);

public sealed record FundPerformanceDto(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public sealed record FundDetailSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionFundDetailWidget";
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Isin { get; init; }
    public string? ShortDescription { get; init; }
    public decimal? Nav { get; init; }
    public decimal? DayChangePercent { get; init; }
    public decimal? Ter { get; init; }
    public DateOnly? InceptionDate { get; init; }
    public string? FactsheetUrl { get; init; }
    public FundPerformanceDto? Performance { get; init; }
    public IReadOnlyList<PricePointDto> Prices { get; init; } = Array.Empty<PricePointDto>();
    /// <summary>True when the configured fund could not be found upstream.</summary>
    public bool Unavailable { get; init; }
}

public sealed record FaqItemDto(string Id, string Question, JsonElement? Answer, string PlainTextAnswer);

public sealed record FaqAccordionSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionFaqAccordion";
    public string? Heading { get; init; }
    public IReadOnlyList<FaqItemDto> Items { get; init; } = Array.Empty<FaqItemDto>();
    public bool EmitFaqSchema { get; init; }
}

public sealed record ImageWithTextSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionImageWithText";
    public string? Heading { get; init; }
    public JsonElement? Body { get; init; }
    public ImageDto? Image { get; init; }
    public string ImagePosition { get; init; } = "left";
    public LinkDto? Cta { get; init; }
}

public sealed record DisclaimerSection : SectionDto
{
    [JsonIgnore]
    public override string Type => "sectionDisclaimer";
    public string? Label { get; init; }
    public JsonElement? Body { get; init; }
    public string Severity { get; init; } = "info";
    public bool Collapsible { get; init; }
}
