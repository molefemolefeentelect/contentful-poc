# SEO and structured data

SEO metadata and JSON-LD are resolved entirely on the BFF and handed to Next.js as
data on the same page response — no page hand-codes its own `<title>`, meta tags or
schema, and the frontend never makes a second request for metadata.

## End-to-end flow: meta tags

```
Contentful `seo` entry (+ siteSettings.defaultSeo as fallback)
  -> SeoResolver.Resolve()          [bff/src/Cms.Bff/Seo/SeoResolver.cs]
  -> PageResponse.Seo               [part of the same /api/pages response as sections]
  -> toMetadata()                   [web/lib/metadata.ts]
  -> Next.js generateMetadata()     [rendered into <head>]
```

**`SeoResolver.Resolve`** (`bff/src/Cms.Bff/Seo/SeoResolver.cs`):

1. Picks a source: the page's own linked `seo` entry if present, else
   `siteSettings.defaultSeo`. This is the fallback chain — a `seo` entry is required
   on the content type, but the resolver still falls back gracefully for an older
   entry that predates the requirement, or if a page's link is somehow unresolved.
2. Runs `{{token}}` substitution on `metaTitle`, `metaDescription`, `ogTitle`,
   `ogDescription` via `Substitute()`. The token pattern is
   `\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}`; on the fund detail template these resolve to
   `{{fund.name}}`, `{{fund.code}}`, `{{fund.price}}`, `{{fund.ter}}`, populated by
   `PageResolutionService.BuildTokens` from the page's resolved fund section. An
   unmatched token is removed rather than left as literal braces in a search result.
3. Falls `ogTitle`/`ogDescription` back to the (already-substituted) `metaTitle`/
   `metaDescription` when blank.
4. Computes the canonical URL: an explicit `canonicalUrl` override on the `seo` entry
   wins; otherwise `BuildCanonical(slug)` builds `{siteBaseUrl}/{slug}` (or just the
   base URL with a trailing slash for the homepage's empty slug).
5. Carries `noindex`/`nofollow` straight through as booleans, and
   `structuredDataType` (defaulting to `"WebPage"`) plus the raw
   `structuredDataOverrides` JSON escape hatch for the JSON-LD side.
6. If a page ends up with no `metaTitle` at all (no page `seo`, no site-wide default),
   `PageResolutionService.BuildAsync` logs a warning and falls back to the page's
   `title` field so the response is never sent with an empty title.

**`toMetadata`** (`web/lib/metadata.ts`) maps the resulting `SeoDto` (camelCased to
`Seo` on the TypeScript side) onto Next's `Metadata` type: `title`, `description`,
`alternates.canonical`, `robots.{index,follow}` (mirrored into `robots.googleBot`),
and `openGraph`/`twitter` blocks including the resolved `ogImage`.

It also accepts an optional `MetadataOptions` (`canonicalOverride`, `forceNoIndex`)
used by the indexation logic below. If both are set on the same call, it logs a
console error, because that combination should never actually happen — see
"Canonical and noindex are never combined" below.

## End-to-end flow: JSON-LD

```
Contentful `seo.structuredDataType` (+ structuredDataOverrides escape hatch)
  -> JsonLdBuilderRegistry.Build()  [bff/src/Cms.Bff/Seo/JsonLd/JsonLdBuilderRegistry.cs]
  -> PageResponse.JsonLd[]          [array, same response as sections/seo]
  -> <JsonLd> component             [web/components/seo/JsonLd.tsx]
```

`JsonLdBuilderRegistry.Build` looks up an `IJsonLdBuilder` by the resolved SEO's
`StructuredDataType` (`WebPage`, `Organization`, `Article`, `Product`, `FAQPage`,
`BreadcrumbList`, or `None`). If found, it builds the schema object and merges in
`structuredDataOverrides` — a raw JSON escape hatch whose top-level keys overwrite the
generated schema's matching keys (`Merge` in the same file). If the type has no
matching builder and isn't `"None"`/empty, it logs a warning and emits nothing for
that page rather than failing the request.

`BreadcrumbList` is additive: whenever a page has more than one breadcrumb entry and
its primary type isn't already `BreadcrumbList`, a second schema object is appended
for the breadcrumb trail (`bff/src/Cms.Bff/Seo/JsonLd/BreadcrumbJsonLdBuilder.cs`),
built from `page.breadcrumbParent`'s authored chain rather than by splitting the URL.

Per-type builders read from the section tree and the resolved SEO, not just the `seo`
entry directly — e.g. `ProductJsonLdBuilder` builds a schema.org `Product` from the
first non-unavailable `sectionFundDetailWidget` on the page (`fund.Name`, ISIN as a
`PropertyValue` identifier, NAV as an `Offer` in ZAR), and warns if a page somehow
carries more than one fund detail widget, since only the first is represented.
`Organization`/`WebSite` schema is emitted sitewide from `siteSettings`, independent
of any one page's `structuredDataType`.

The `<JsonLd>` component (`web/components/seo/JsonLd.tsx`) serialises each object into
its own `<script type="application/ld+json">`, escaping `<`, `>` and `&` to
`<`/`>`/`&` so admin-authored content (including anything that slipped
through `structuredDataOverrides`) can never break out of the script tag.

## Indexation decision table

Filtered listing pages (currently just `/funds?categoryId=...`) get an explicit,
stated indexation policy rather than an ambiguous one, implemented in
`web/lib/indexation.ts`'s `resolveIndexation` and applied from
`web/app/[[...slug]]/page.tsx`'s `generateMetadata`:

| URL shape | Treatment | Why |
|---|---|---|
| `/funds` (no query params) | Indexable, self-canonical | The base listing is the canonical page. |
| `/funds?page=2` (pagination only) | Indexable, self-canonical | Genuine pagination — page 2 holds different funds, not a near-duplicate of page 1. |
| `/funds?categoryId=<known id>` (single, recognised filter) | Indexable, canonical → `/funds` | A near-duplicate of the base listing; canonicalizing avoids diluting ranking signal across near-identical URLs while still letting the filtered URL be crawled. |
| `/funds?categoryId=<unrecognised id>` | `noindex, follow` | An id the current widget instance can't actually render as a known category — indexing it would index a broken or empty view. |
| `/funds?categoryId=1&categoryId=2` (repeated param) | `noindex, follow` | A different content set from any single category — not a near-duplicate, so it isn't canonicalized, but it's also not a page worth indexing on its own. |
| Any other or additional param (sort, pageSize, arbitrary combinations) | `noindex, follow` | Prevents an unbounded number of near-duplicate parameter combinations from entering the index. |

`knownCategoryIds` is deliberately the set of categories *this specific widget
instance* can render (`section.categories.map(c => c.id)` from the resolved
`FundListSection` on that page), not a global category list — a category that's valid
site-wide but not renderable by this particular widget is correctly treated as
unrecognised rather than wrongly canonicalized to a page that wouldn't show it.

## Why canonical and noindex are never combined

Google discards the canonical tag on a page that is also `noindex` — the two signals
are contradictory, and search engines resolve the conflict unpredictably.
`resolveIndexation` never returns both `canonicalOverride` and `forceNoIndex` set at
the same time; every branch in the table above picks exactly one. `toMetadata` treats
this as an invariant rather than trusting every caller to get it right: if a future
caller ever violates it, `index` is derived purely from `noIndex`/`forceNoIndex` and
never from the presence of a canonical override, and a console error is logged. In
other words, if the two ever disagree, noindex wins — silently failing to
canonicalize a page is safe, silently indexing content an author wanted hidden is not.

## Crawler-degradation choices

Built in from the start rather than retrofitted, and verified against the actual
components:

- **FAQ accordion** (`web/components/sections/FaqAccordion.tsx`) uses native
  `<details>`/`<summary>` elements. Every answer is present in the server-rendered DOM
  whether the accordion item is open or collapsed — no client-side toggle hides
  content from a crawler.
- **Category filters on the fund list** (`web/components/sections/FundListWidget.tsx`,
  applied server-side in `web/app/[[...slug]]/page.tsx`) render as real `<Link
  href="/funds?categoryId=...">` elements, and the catch-all page route filters the
  fund list server-side from the `categoryId` query param before rendering — so a
  crawler following the link gets a genuinely filtered, server-rendered page, not a
  client-only re-render.

As built, there is no tabbed panel UI and no `?page=2` pagination control actually
wired into the fund list or fund detail widgets — the fund list widget renders every
fund the resolver returns (bounded by the section's authored `topN`), and the fund
detail widget renders all its fields (summary, performance table, price-history
count, factsheet link) inline on one page rather than behind tabs. `resolveIndexation`
already has a rule for genuine pagination (`?page=2`, self-canonical) so the
indexation policy is ready for it, but the pagination control itself is not part of
this PoC.
