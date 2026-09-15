namespace Cms.Bff.Seo.JsonLd;

public interface IJsonLdBuilder
{
    /// <summary>The seo.structuredDataType value this builder handles.</summary>
    string StructuredDataType { get; }

    /// <summary>Returns null when this page has nothing to emit.</summary>
    Dictionary<string, object?>? Build(JsonLdContext context);
}
