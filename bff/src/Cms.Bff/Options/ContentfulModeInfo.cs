namespace Cms.Bff.Options;

/// <summary>
/// The Contentful mode ("Live" or "Fixture") actually used to choose the wired
/// IContentfulClient implementation at startup, captured once so /health can report
/// exactly what is serving requests instead of re-deriving it from IOptions&lt;ContentfulOptions&gt;,
/// which caches its value independently and could theoretically disagree.
/// </summary>
public sealed record ContentfulModeInfo(string Mode);
