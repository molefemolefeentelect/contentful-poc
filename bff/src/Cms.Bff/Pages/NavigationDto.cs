using System.Text.Json;
using Cms.Bff.Sections;

namespace Cms.Bff.Pages;

public sealed record NavigationItemDto(string Label, string Url, bool External, IReadOnlyList<NavigationItemDto> Children);

public sealed record NavigationDto(string Key, IReadOnlyList<NavigationItemDto> Items);

public sealed record SocialLinkDto(string Label, string Url);

public sealed record SiteSettingsResponse
{
    public required string SiteName { get; init; }
    public ImageDto? Logo { get; init; }
    public JsonElement? DisclaimerText { get; init; }
    public IReadOnlyList<SocialLinkDto> SocialLinks { get; init; } = Array.Empty<SocialLinkDto>();
    public IReadOnlyList<NavigationDto> Navigations { get; init; } = Array.Empty<NavigationDto>();
    public IReadOnlyList<Dictionary<string, object?>> JsonLd { get; init; } = Array.Empty<Dictionary<string, object?>>();
}
