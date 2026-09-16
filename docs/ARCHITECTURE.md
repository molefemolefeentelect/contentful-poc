# Architecture

## The page-as-composed-sections model

A `page` entry in Contentful is deliberately thin: a title, a slug, a `pageType`
(`marketing` | `dataDriven`), a required link to a `seo` entry, an optional
`breadcrumbParent` self-reference, and — the important part — `sections`, an ordered
array of references to any of ten section content types (`sectionHero`,
`sectionRichText`, `sectionCardGrid`, `sectionCtaBanner`, `sectionArticleTeaserList`,
`sectionFundListWidget`, `sectionFundDetailWidget`, `sectionFaqAccordion`,
`sectionImageWithText`, `sectionDisclaimer`). Contentful's reference-field validation
whitelists exactly those content types, so an editor cannot assemble an invalid page
even by accident — there's no separate approval step enforcing that.

This removes the developer from page creation entirely for the common case: an admin
user builds a new page by picking a title, a slug, an SEO entry, and dragging existing
section entries into an order — the same building blocks used across the rest of the
site. Composing a page from existing section types requires zero code changes. Adding
a genuinely new section *type* still requires code (see below), but that's a rare
event compared to shipping pages.

## Keeping data-driven sections decoupled

Two of the ten section types — `sectionFundListWidget` and `sectionFundDetailWidget` —
render live fund data, but they store **configuration only** in Contentful: which
category, which fund code, which fields to show (performance, price history,
factsheet link). No fund name, price or performance number is ever written to
Contentful.

The division of responsibility:

- **Contentful** owns configuration: which widget, on which page, showing which
  fields, for which fund/category.
- **The Fund Data API** (`Cms.FundData.Api`, port 5090) owns the data: fund names,
  NAV, day change, TER, performance, price history. It knows nothing about Contentful
  or pages.
- **The BFF** (`Cms.Bff`, port 5080) is the merge point. `FundListSectionResolver` and
  `FundDetailSectionResolver` read the widget's Contentful configuration, call
  `IFundDataClient` for the matching live data, and combine them into a single section
  DTO before the frontend ever sees it. The frontend never talks to the Fund Data API
  directly.

This means a fund's price can change every day without touching Contentful, and an
admin user can restyle or reposition a fund widget without touching fund data.

## Publish → webhook → BFF eviction → Next revalidate → live update

When an admin user publishes an entry in Contentful, the following sequence brings the
live site up to date, typically within a few seconds and with no deployment:

1. Contentful fires a webhook to `POST {bff}/api/webhooks/contentful` with the
   published entry's `sys.id` in the body. `WebhookEndpoints` requires an
   `X-Webhook-Secret` header matching `Integration:WebhookSecret`; a missing or wrong
   secret gets `401 Unauthorized` and nothing else happens.

2. The BFF looks up that entry id in `CacheTagStore`, a reverse index
   (`entryId → set of cache keys`) populated whenever a page or article was resolved
   and cached. Cache keys look like `page:{slug}|fund:{fundCode|-}` (e.g.
   `page:about|fund:-`, or `page:funds/_detail|fund:ABC` for a fund rendered through
   the shared template) — see `PageResolutionService.GetPageAsync` and `PageCache`.
   Only the cached payloads that actually embed the changed entry are evicted from
   `IMemoryCache`; everything else stays warm. `PageCache.InvalidateEntry` returns the
   list of evicted keys.

3. The BFF calls `POST {web}/api/revalidate` with `{ secret, entryId, tags: evicted }`,
   where `secret` is `Integration:PreviewSecret`. If this call fails (Next.js
   unreachable), the BFF logs the error and stops — its own cache is already clear, so
   the BFF-side data is correct; the Next.js side self-heals on its next
   time-based ISR pass (`revalidate = 300` on the affected routes) even without this
   call succeeding.

4. Next's `/api/revalidate` route (`web/app/api/revalidate/route.ts`) validates the
   secret, then does two things. First, it unconditionally calls `revalidateTag` on
   the broad tags `"pages"`, `"articles"`, `"site"`, `"funds"` — any of these might
   embed the changed entry indirectly (e.g. a nav item), so this is deliberately
   coarse. Second, it parses each narrow key the BFF sent: a key of the form
   `page:{slug}|fund:{code}` is **not** revalidated as `page:funds/_detail` — the BFF
   caches every fund under the one template's cache key, but Next.js fetched and
   rendered that fund at `/funds/{code}` tagged `fund:{code}` (see
   `getFundDetailPage` in `web/lib/bff.ts`). The revalidate route detects the
   `fund:{code}` suffix and calls `revalidateTag('fund:{code}')` plus
   `revalidatePath('/funds/{code}')` instead of anything referencing
   `funds/_detail`. An ordinary page key becomes `revalidateTag('page:{slug}')` plus
   `revalidatePath('/{slug}')` (or `/` for the homepage's empty slug). It also calls
   `revalidatePath('/sitemap.xml')` unconditionally.

5. The next request to the affected URL — whether that's an actual visitor or the
   crawler that re-requests the sitemap — gets freshly rendered content. No rebuild
   and no redeploy occurred; only cache entries were invalidated on both the BFF and
   Next.js sides.

Preview requests bypass steps 2–4 entirely: `PageCache.GetOrCreateAsync` never caches
or tags a preview render, so a draft always resolves live against the Contentful
Preview API.

## The fund detail template mechanism

`/funds/{code}` is a dedicated Next.js route (`web/app/funds/[code]/page.tsx`), not
the catch-all. Rather than requiring an admin user to hand-author one `page` entry per
fund — which would defeat the point of a data-driven page — every fund code is served
by **one** Contentful `page` entry at the reserved slug `funds/_detail`
(`PageResolutionService.FundDetailTemplateSlug`).

The mechanics:

- The Next.js route calls the BFF for the page at slug `funds/_detail`, passing the
  URL's `{code}` as `fundCode`.
- `PageResolutionService.BuildAsync` passes that code into `SectionContext` as
  `FundCodeOverride`. `FundDetailSectionResolver` uses the widget's own authored
  `fundCode` field if present, and only falls back to the route's code when the field
  is blank — so an explicitly authored `fundCode` always wins, keeping a bespoke
  single-fund landing page possible alongside the shared template.
- Because the resolved page still carries slug `funds/_detail` internally, but must
  present as `/funds/{code}` externally, `EffectiveSlug` rewrites the slug used for
  canonical-URL and cache-key purposes: `slug == FundDetailTemplateSlug && fundCode
  present` becomes `funds/{code.ToLowerInvariant()}`. This is the one place in the
  system where a section's configuration is overridden at request time rather than
  purely by authored content.
- SEO gets fund-aware for free: `PageResolutionService.BuildTokens` extracts
  `{{fund.name}}`, `{{fund.code}}`, `{{fund.price}}`, `{{fund.ter}}` from the resolved
  fund detail section, and `SeoResolver.Resolve` substitutes them into the template's
  admin-authored `metaTitle`/`metaDescription`/OG fields — so one entry like
  `"{{fund.name}} — Fund Details"` produces a distinct, correct title per fund without
  per-fund authoring.
- A direct read of the raw template slug without a `fundCode` is rejected (see
  `bb4c72f` in the commit history) rather than rendering a template page with no real
  fund behind it.

## Two degradation contracts

**1. Unknown section type: dropped and logged, never fatal.** Both sides of the stack
mirror this contract independently:

- BFF: `SectionResolverRegistry.ResolveOneAsync` looks up a resolver by the section
  entry's Contentful content-type id. If none is registered, it logs a warning
  (`"No resolver registered for section content type ... Section dropped."`) and
  returns `null` for that section rather than throwing. A resolver that throws is also
  caught here and logged, and drops just that section — a page with nine good
  sections and one broken one still renders the other nine.
- Frontend: `SectionRenderer` looks up each resolved section's `__type` in
  `components/sections/registry.ts`. An unmapped type renders nothing in production,
  and a visible amber diagnostic block in development (`"No component registered for
  section type ..."`). This exists specifically so a frontend deployed behind a newer
  content model (a section type the BFF resolves but this build doesn't yet render)
  degrades safely instead of crashing.

A half-migrated content model — a new section type added to Contentful before the
matching code ships — must not 500 the whole page on either side.

**2. Fund Data API outage: degrades a section, not the page.** `FundListSectionResolver`
and `FundDetailSectionResolver` each wrap their `IFundDataClient` calls in a
`try/catch` that the section resolvers handle themselves (the exception never reaches
`SectionResolverRegistry`'s own catch, so each resolver logs it directly or the outage
would leave no trace). On failure:

- The fund list widget degrades to an empty fund list, but its authored heading, intro
  and any filter chips still render.
- The fund detail widget returns `{ Unavailable: true }` with whatever code is known;
  `FundDetailWidget` renders "Fund data is temporarily unavailable." instead of
  numbers, and the fund detail *route* (`web/app/funds/[code]/page.tsx`) treats an
  unavailable widget as `notFound()` for that specific URL — the rest of the site,
  including other funds and every marketing page, is unaffected.

This mirrors a related but distinct degradation applied to BFF-outage scenarios on the
Next.js side: `web/app/layout.tsx` wraps `getSiteSettings()` in try/catch so a BFF
outage renders the site with default nav/footer rather than crashing the whole app;
`web/app/sitemap.ts` and the catch-all route's `generateStaticParams` (in
`web/app/[[...slug]]/page.tsx`) both wrap their BFF sitemap calls the same way, because
an uncaught throw in either would fail the entire `next build`/ISR regeneration rather
than just one page.

## A note on fixtures versus the real thing

The BFF's `IContentfulClient` has two implementations selected by `Contentful:Mode`:
`FixtureContentfulClient` reads `contentful/seed/fixtures/entries.json` in the same
shape the real Content Delivery API returns (including `includes` and `Link` nodes),
and `HttpContentfulClient` talks to the real CDA/CPA. Fixtures let the whole stack be
exercised, tested and demoed with no Contentful account.

They are not a substitute for testing against a live space, though. Wiring the BFF to
a real Contentful space during this build surfaced three bugs that fixtures never
would have caught, because fixtures encode the shape the code expects rather than the
shape a real dependency actually returns:

- `contentful-management` v12 defaults to a flat "plain" client API rather than the
  chainable `getSpace()`/`getEnvironment()` shape the seed script was written against.
- Contentful's Content Delivery API silently drops a query parameter whose value is
  the empty string, so an exact-match filter on `fields.slug=` could never find the
  homepage (whose slug genuinely is `""`). `PageResolutionService.FetchPageEntryAsync`
  now special-cases the empty slug by fetching all pages and matching client-side.
  See the inline comment there.
- The Fund Data API's fund-list endpoint nests one-year return under
  `performance.oneYear`, not a flat `oneYearReturn` field — the same shape it uses on
  the fund-detail endpoint. `System.Text.Json` deserialized this silently into a
  default of `0` instead of throwing, so every fund in every list showed a 0.0% 1-year
  return until `HttpFundDataClient` was fixed to read the real wire shape.

None of these were reachable with fixtures alone, which is why this project treats
fixture-mode tests and a periodic real-Contentful pass as complementary, not
interchangeable.
