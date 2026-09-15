namespace Cms.Bff.Options;

public sealed class ContentfulOptions
{
    public const string Section = "Contentful";

    /// <summary>Live = real CDA/CPA. Fixture = read seeded JSON, no account needed.</summary>
    public string Mode { get; set; } = "Fixture";
    public string SpaceId { get; set; } = "";
    public string Environment { get; set; } = "master";
    public string DeliveryToken { get; set; } = "";
    public string PreviewToken { get; set; } = "";
    public string FixturePath { get; set; } = "../../../contentful/seed/fixtures/entries.json";
    public int CacheSeconds { get; set; } = 300;
}

public sealed class FundDataOptions
{
    public const string Section = "FundData";
    public string BaseUrl { get; set; } = "http://localhost:5090";
}

public sealed class SiteOptions
{
    public const string Section = "Site";
    /// <summary>Absolute origin used to build canonical URLs. No trailing slash.</summary>
    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string DefaultOgImage { get; set; } = "";
}

public sealed class IntegrationOptions
{
    public const string Section = "Integration";
    /// <summary>Shared secret Contentful's webhook must send in X-Webhook-Secret.</summary>
    public string WebhookSecret { get; set; } = "dev-webhook-secret";
    /// <summary>Shared secret guarding the preview and revalidate handshakes.</summary>
    public string PreviewSecret { get; set; } = "dev-preview-secret";
    /// <summary>Next.js on-demand revalidation endpoint.</summary>
    public string RevalidateUrl { get; set; } = "http://localhost:3000/api/revalidate";
}
