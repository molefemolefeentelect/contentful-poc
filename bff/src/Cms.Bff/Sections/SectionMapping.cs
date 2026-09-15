using Cms.Bff.Contentful;

namespace Cms.Bff.Sections;

public static class SectionMapping
{
    /// <summary>
    /// Maps a mediaImage entry to an ImageDto. Alt text is required by the content
    /// model; the empty-string fallback exists only for entries created before the
    /// validation was added.
    /// </summary>
    public static ImageDto? ToImage(ResolvedEntry? media)
    {
        var asset = media?.GetAsset("image");
        if (media is null || asset is null) return null;

        return new ImageDto(
            asset.Url,
            media.GetString("altText") ?? "",
            asset.Width,
            asset.Height,
            media.GetString("caption"));
    }

    public static LinkDto? ToLink(string? label, string? url) =>
        string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(url)
            ? null
            : new LinkDto(label, url);
}
