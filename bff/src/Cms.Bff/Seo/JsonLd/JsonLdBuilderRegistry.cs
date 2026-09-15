using System.Text.Json;

namespace Cms.Bff.Seo.JsonLd;

public sealed class JsonLdBuilderRegistry
{
    private readonly Dictionary<string, IJsonLdBuilder> _builders;
    private readonly ILogger<JsonLdBuilderRegistry> _logger;

    public JsonLdBuilderRegistry(IEnumerable<IJsonLdBuilder> builders, ILogger<JsonLdBuilderRegistry> logger)
    {
        _builders = builders.ToDictionary(b => b.StructuredDataType, StringComparer.Ordinal);
        _logger = logger;
    }

    public IReadOnlyList<Dictionary<string, object?>> Build(JsonLdContext context)
    {
        var results = new List<Dictionary<string, object?>>();

        if (_builders.TryGetValue(context.Seo.StructuredDataType, out var builder))
        {
            var built = builder.Build(context);
            if (built is not null) results.Add(Merge(built, context.Seo.StructuredDataOverrides));
        }
        else if (context.Seo.StructuredDataType is not ("None" or ""))
        {
            _logger.LogWarning(
                "No JSON-LD builder for structured data type '{Type}' on page '{Slug}'. No schema emitted.",
                context.Seo.StructuredDataType, context.Slug);
        }

        // BreadcrumbList is additive: it accompanies whatever the page's primary type is.
        if (context.Breadcrumbs.Count > 1 &&
            _builders.TryGetValue("BreadcrumbList", out var crumbs) &&
            context.Seo.StructuredDataType != "BreadcrumbList")
        {
            var built = crumbs.Build(context);
            if (built is not null) results.Add(built);
        }

        return results;
    }

    private static Dictionary<string, object?> Merge(Dictionary<string, object?> generated, JsonElement? overrides)
    {
        if (overrides is not { ValueKind: JsonValueKind.Object } obj) return generated;

        foreach (var property in obj.EnumerateObject())
            generated[property.Name] = ToClrValue(property.Value);

        return generated;
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.Clone(),
    };
}
