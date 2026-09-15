namespace Cms.Bff.Contentful;

public sealed record ContentfulQuery(
    string ContentType,
    IReadOnlyDictionary<string, string>? Filters = null,
    int Include = 6,
    int Limit = 100,
    string? Order = null);

public interface IContentfulClient
{
    /// <param name="preview">true routes to the Preview API and returns drafts.</param>
    Task<CdaResponse> QueryAsync(ContentfulQuery query, bool preview, CancellationToken ct = default);
}
