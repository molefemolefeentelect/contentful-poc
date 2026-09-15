using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed class FaqPageJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "FAQPage";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        var items = context.Sections
            .OfType<FaqAccordionSection>()
            .Where(s => s.EmitFaqSchema)
            .SelectMany(s => s.Items)
            .ToList();

        if (items.Count == 0) return null;

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = items.Select(i => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = i.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer", ["text"] = i.PlainTextAnswer,
                },
            }).ToList(),
        };
    }
}
