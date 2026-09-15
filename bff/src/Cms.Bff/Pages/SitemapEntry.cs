namespace Cms.Bff.Pages;

public sealed record SitemapEntry(string Loc, DateTimeOffset? LastModified, string ChangeFrequency, double Priority);
