# Composable, Admin-Managed Website PoC — Design

Date: 2026-09-15
Status: Approved

## 1. Goal

Prove that a non-technical **admin user** (marketer / content ops at a South African
fund/ETF manager) can, working **entirely inside Contentful's own web app**, with no
code change and no developer-triggered deployment:

1. Create a brand new page at a brand new URL slug
2. Compose that page from a library of reusable content blocks
3. Edit, reorder and remove blocks on an existing page
4. Publish / unpublish and schedule changes
5. Preview unpublished changes on the real Next.js site (Next.js Draft Mode driven by
   Contentful's Preview API — not a bespoke preview UI)
6. Manage that page's SEO — metadata, structured data, indexability — unaided

Reference site for page types and content patterns: https://satrix.co.za/

### Terminology (load-bearing)

- **Contentful** is the CMS *and* the authoring interface. We build no second admin UI.
- **Admin user** is the human authoring in Contentful. They never open the repo.
- The **Next.js app is a renderer only**. It fetches whatever tree the BFF returns and
  displays it. It has no "edit this page" mode.

### Success criteria

- A page created only in Contentful is live at its slug after publish, with no deploy.
- Reordering sections on the homepage in Contentful reorders them on the live site.
- Preview shows unpublished drafts on the real site at the real URL.
- `<head>` metadata and JSON-LD for every page trace back to admin-editable fields.
- Adding a *new kind of block* is the only change that requires a developer.

## 2. Decisions taken

| Decision | Choice | Rationale |
|---|---|---|
| Hosting | Local + tunnel (ngrok/cloudflared) | Webhooks and Contentful preview need a publicly reachable URL; cheapest route to proving the full loop |
| Contentful space | Build now, wire tokens later | Migrations + full integration written; `Fixture` mode makes the stack demoable today |
| BFF ↔ frontend contract | REST | BFF already aggregates; per-page payload is fixed, so GraphQL's flexibility would go unused |
| Next.js flavour | App Router, Next 15 | `generateMetadata`, `draftMode()`, `revalidateTag`, RSC-by-default map directly onto the SEO requirements |
| Testing | Targeted tests on risky logic | Section resolution, SEO merge, cache invalidation, registry degradation |
| Styling | Tailwind, minimal | Pixel-perfect design is an explicit non-goal |

## 3. Repository layout

```
cms-poc/
  contentful/
    migrations/           contentful-migration scripts, numbered and idempotent
    seed/                 seed script + CDA-shaped fixture JSON
  bff/
    Cms.sln
    src/Cms.Bff/          ASP.NET Core minimal API (.NET 10)
    src/Cms.FundData.Api/ mock upstream fund/product data API, own port
    tests/Cms.Bff.Tests/  xUnit
  web/                    Next.js 15, App Router, TypeScript, Tailwind
  docs/
    ARCHITECTURE.md
    SEO.md
    DEMO.md
  README.md
```

`Cms.FundData.Api` is a **separate project on its own port**, not a controller inside
the BFF. The boundary between "Contentful stores composition and configuration" and
"the product data API owns transactional data" is then visible in the process list, not
merely asserted in prose.

## 4. Contentful content model

### 4.1 Composition pattern

`page` holds `sections: Reference (many)` whose validation whitelists exactly the
allowed section content types. The admin user composes by adding, dragging and removing
referenced entries. Because the whitelist lives in the content model, an invalid layout
cannot be assembled in the first place.

### 4.2 Content types

**`seo`** (reusable, referenced — not an inline field group)

| Field | Type | Validation |
|---|---|---|
| internalName | Symbol | required |
| metaTitle | Symbol | required, max 60 |
| metaDescription | Text | required, max 160 |
| ogTitle | Symbol | optional override |
| ogDescription | Text | optional override |
| ogImage | Link → mediaImage | optional |
| canonicalUrl | Symbol | optional, URL regex |
| noindex | Boolean | default false |
| nofollow | Boolean | default false |
| structuredDataType | Symbol | in [WebPage, Organization, Article, Product, FAQPage, BreadcrumbList, None] |
| structuredDataOverrides | Object (JSON) | optional escape hatch |

Referenced as `Reference (one, required)` from `page` and `article`.
`siteSettings.defaultSeo` supplies the fallback, which is how "required-with-fallback"
is realised: the field is required on the type, and the resolver still falls back if an
older entry predates the requirement.

**`mediaImage`** — the alt-text enforcement mechanism

| Field | Type | Validation |
|---|---|---|
| internalName | Symbol | required |
| image | Link → Asset | required |
| altText | Symbol | **required**, max 125 |
| caption | Symbol | optional |

Every image reference in every section type points at `mediaImage` rather than at an
Asset directly. Contentful cannot make an Asset's description mandatory; a required
field on a shared content type is the only way to actually enforce alt text. Accepted
cost: one extra entry per image.

**`page`**

| Field | Type | Validation |
|---|---|---|
| title | Symbol | required |
| slug | Symbol | required, unique, lowercase/hyphen/slash regex; empty string = homepage |
| pageType | Symbol | in [marketing, dataDriven] |
| seo | Link → seo | required |
| sections | Array&lt;Link → section types&gt; | whitelist validation |
| breadcrumbParent | Link → page | optional, self-reference |

`breadcrumbParent` makes BreadcrumbList authored rather than inferred by splitting a
URL string.

**Section types**

- `sectionHero` — eyebrow, heading, subheading, backgroundImage (mediaImage), primaryCta, secondaryCta, variant
- `sectionRichText` — heading, body (RichText), width variant
- `sectionCardGrid` — heading, intro, cards (many → `card`), columns
- `card` — title, body, image (mediaImage), link, icon
- `sectionCtaBanner` — heading, body, cta, variant
- `sectionArticleTeaserList` — heading, mode (latest | manual), category filter, limit, manual article refs
- `sectionFundListWidget` — heading, intro, categoryId, topN, displayVariant, showFilters *(configuration only)*
- `sectionFundDetailWidget` — fundCode, showPerformance, showPriceHistory, showFactsheet *(configuration only)*
- `sectionFaqAccordion` — heading, items (many → `faqItem`), emitFaqSchema
- `faqItem` — question, answer (RichText)
- `sectionImageWithText` — heading, body, image (mediaImage), imagePosition, cta
- `sectionDisclaimer` — label, body (RichText), severity (info | warning), collapsible

`sectionDisclaimer` is an addition to the brief. The reference site runs a compliance
band and a fraud-awareness notice on nearly every page; modelling that as its own type
beats overloading rich text, and it lets compliance copy be swapped globally.

Fund widgets carry **configuration only**. No fund name, price or performance figure is
ever stored in Contentful.

**Editorial content**

- `article` — title, slug, seo (required), excerpt, body (RichText), featuredImage
  (mediaImage), author (→ `author`), category (→ `category`), tags, publishDate,
  updatedDate, relatedArticles
- `author` — name, jobTitle, bio, photo (mediaImage)
- `category` — name, slug

**Global**

- `navigation` — name, key (header | footer), items (many → `navigationItem`)
- `navigationItem` — label, page (→ page/article), externalUrl, children (many →
  navigationItem, one level)
- `siteSettings` — siteName, logo (mediaImage), socialLinks (JSON), disclaimerText
  (RichText), legalFooterLinks, defaultSeo (→ seo), organizationLegalName, organizationSameAs

### 4.3 Known model gap: cross-type slug collisions

Contentful's uniqueness validation is **per content type**. It cannot prevent a `page`
with slug `market-outlook` colliding with an `article` of the same slug.

Mitigation: namespace the URL space. Articles resolve under `/news/{slug}`, funds under
`/funds/{code}`, pages at root. The BFF resolves in fixed precedence (page → article →
fund) and logs a warning on ambiguity. Documented in the README as a real constraint,
not silently ignored.

## 5. .NET BFF

### 5.1 Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /api/pages/{*slug}` | Resolved page tree: `{ slug, pageType, title, seo, jsonLd[], breadcrumbs[], sections[] }` |
| `GET /api/articles/{slug}` | Article + resolved SEO |
| `GET /api/articles?category=&page=` | Paginated listing |
| `GET /api/navigation` | Header + footer trees |
| `GET /api/site-settings` | Global settings incl. default SEO |
| `GET /api/sitemap` | `{ loc, lastmod, changefreq, priority }[]` across pages, articles, funds |
| `POST /api/webhooks/contentful` | Secret-validated; evicts by tag, then calls Next revalidate |
| `GET /api/preview` | Preview token broker/validator called by Next's draft route |
| `GET /health` | Liveness |

The page response carries the resolved SEO object **alongside** the section tree. The
frontend never makes a second call for metadata.

### 5.2 Resolution pipeline

1. `IContentfulClient` fetches the page by slug with `include=6`, against the Delivery
   API or the Preview API depending on `PreviewContext`.
2. `EntryLinkResolver` flattens Contentful's `includes` block into a lookup and resolves
   `sys.type == "Link"` nodes recursively, with a cycle guard and depth cap.
3. `SectionResolverRegistry` dispatches each section entry by content-type id to an
   `ISectionResolver`, each emitting a DTO with a `__type` discriminator.
4. An **unregistered content type is dropped and logged, never thrown** — the same
   degradation contract the frontend registry honours. A half-migrated content model
   must not 500 the whole page.
5. Fund widget resolvers call `IFundDataClient` and run in parallel via `Task.WhenAll`.
6. `ISeoResolver` merges, in order: page SEO → `siteSettings.defaultSeo` → computed
   canonical (`siteUrl + slug`). It then performs token substitution — `{{fund.name}}`,
   `{{fund.price}}`, `{{fund.code}}` — so a fund detail page's title and Product JSON-LD
   carry live data while remaining entirely admin-authored.
7. `IJsonLdBuilder` implementations emit JSON-LD per `structuredDataType`, merged with
   `structuredDataOverrides`. Organization + WebSite are emitted sitewide from
   `siteSettings`.

### 5.3 Caching and invalidation

`IMemoryCache` behind a caching decorator, plus a **reverse index** (`CacheTagStore`:
`entryId → set of cache keys`) populated during resolution. A webhook naming entry X
evicts exactly the cached pages that embed X, rather than flushing everything. Preview
requests bypass cache entirely.

Redis is a drop-in swap behind the same interface; in-memory is sufficient for the PoC
and is called out as such.

### 5.4 Fixture mode

`IContentfulClient` has two implementations:

- `HttpContentfulClient` — real CDA/CPA
- `FixtureContentfulClient` — reads `contentful/seed/*.json` in genuine CDA response
  shape, including `includes` and `Link` nodes, so the link resolver is exercised
  identically

Selected by `Contentful:Mode = Live | Fixture`. The stack is demoable before any token
exists; adding tokens and flipping one env var makes it live.

### 5.5 Preview flow — mechanics corrected

Cookies are per-origin, so a BFF-set cookie cannot enable Next.js draft mode. The
working flow is:

```
Contentful "Open preview"
  -> GET {NEXT}/api/draft?secret=...&slug=...
  -> Next validates secret (via BFF /api/preview), draftMode().enable(), 307 redirect
  -> Next renders /slug; its fetch layer sends preview=true to the BFF
  -> BFF routes to Contentful Preview API, bypasses cache
```

The BFF's `/api/preview` endpoint exists as specified, acting as token broker and
validator rather than as the cookie setter.

## 6. Mock Fund Data API

`Cms.FundData.Api`, seeded from JSON with roughly 20 South African ETFs and unit trusts
(name, code, ISIN, category, TER, NAV, inception date, factsheet URL), 90 days of
synthetic price history, and 1y/3y/5y performance.

| Endpoint | Purpose |
|---|---|
| `GET /funds?categoryId=&top=` | Fund list by category |
| `GET /funds/{code}` | Fund detail by code or ISIN |
| `GET /funds/{code}/prices?days=` | Daily price history |
| `GET /categories` | Fund categories |

No real market data integration. Explicit non-goal.

## 7. Next.js renderer

### 7.1 Routing and rendering

- `app/[[...slug]]/page.tsx` — catch-all; `generateStaticParams` seeded from the BFF
  sitemap endpoint → SSG, with ISR and on-demand `revalidateTag`.
- All indexable content is server-rendered. No indexable content is client-only.
- `app/news/[slug]/page.tsx` and `app/funds/[code]/page.tsx` are dedicated routes.
  Next.js matches more specific routes before the optional catch-all, so these take
  precedence without conflicting with it.

**Which pages go through which route — resolved explicitly:**

| URL | Route | Where composition comes from |
|---|---|---|
| `/`, `/about`, `/how-to-invest`, `/tax-free-investing`, `/funds`, `/news` | catch-all | A Contentful `page` entry whose slug matches the URL exactly |
| `/news/{slug}` | dedicated | The `article` entry, rendered by a fixed article layout |
| `/funds/{code}` | dedicated | A Contentful `page` **template** entry at reserved slug `funds/_detail`, whose `sectionFundDetailWidget` inherits `fundCode` from the route param |

The fund detail template is the one place a section's configuration is overridden at
request time. Without it, an admin user would have to hand-author one `page` entry per
fund, which defeats the purpose of a data-driven page. The BFF accepts an optional
`?fundCode=` on `GET /api/pages/funds/_detail` and injects it into any
`sectionFundDetailWidget` whose own `fundCode` is blank; an explicitly authored
`fundCode` always wins, so a bespoke single-fund landing page remains possible.

`/funds` itself stays a normal Contentful page: admin-authored intro, disclaimers and
CTAs, with a `sectionFundListWidget` supplying the cards.

### 7.2 Component registry

`components/sections/registry.ts` maps `__type → React component`. `SectionRenderer`
iterates the section array and looks each type up. An unknown `__type` renders nothing
in production and a visible diagnostic block in development.

**Composing a page from existing section types requires zero code changes. Adding a new
section type requires exactly two: a Contentful content type and a registry entry.**

### 7.3 SEO rendering

- `generateMetadata` maps the BFF's `seo` object to `title`, `description`,
  `openGraph`, `twitter`, `alternates.canonical`, `robots { index, follow }`.
- `<JsonLd>` serialises the BFF's `jsonLd[]` array into `application/ld+json`, with
  `<` escaped to prevent script-breakout. Structured data is never hand-coded per page.
- Root layout emits sitewide Organization + WebSite.
- `app/sitemap.ts` and `app/robots.ts` are dynamic, tagged, and revalidated by the same
  webhook that revalidates pages.

### 7.4 Crawler degradation

Built in from the start, not retrofitted:

- FAQ accordion uses `<details>/<summary>` — answers are in the DOM when collapsed.
- Tabs server-render all panels; JS only controls visibility.
- Fund list server-renders the first N with real `?page=2` links, which JS upgrades to
  a "load more" interaction.

No critical content sits behind client-only interaction without a server-rendered
fallback.

### 7.5 Indexation strategy for filtered pages

A stated decision, not an ambiguity:

| URL shape | Treatment |
|---|---|
| `/funds` | Indexable, self-canonical |
| `/funds?categoryId=3` (known, curated filter) | Indexable, canonical → `/funds` |
| Unrecognised or multi-param combinations (sort, pageSize, arbitrary params) | `noindex, follow` |
| `/funds?page=2` (genuine pagination) | Indexable, **self-canonical** — page 2 is not a near-duplicate |

Canonical and `noindex` are deliberately **never combined on the same URL**. That is
contradictory signalling which search engines resolve unpredictably, and a canonical on
a noindexed page is discarded.

## 8. Testing scope

**xUnit (`Cms.Bff.Tests`)**
- `SectionResolverRegistry` dispatch by content-type id
- Unknown section type is dropped and logged, page still resolves
- `EntryLinkResolver`: nested links, missing links, circular references
- Fund widget resolution merges live data into the section payload
- `SeoResolver` fallback chain and token substitution
- `JsonLdBuilder` per structured-data type, plus overrides merge
- `CacheTagStore` evicts exactly the pages embedding a changed entry

**Vitest (`web`)**
- Registry returns a safe fallback for an unknown `__type`
- `seo → Metadata` mapping, including robots and canonical
- JSON-LD serialisation escaping

Styling and plumbing are not tested. Explicit scope limit.

## 9. Non-goals

Real market data integration; app authentication; a full design system or
pixel-perfect styling; Contentful RBAC, approval workflows and localisation; any custom
authoring or admin screen in the Next.js app or BFF.

## 10. What a non-PoC build would add

RBAC and approval workflows in Contentful; localisation; component-level style variants;
analytics and A/B testing driven from Contentful; **redirect management for slug
changes** (a genuine gap — changing a published slug today breaks inbound links and
rankings with no 301); Core Web Vitals monitoring; Redis-backed distributed cache;
structured logging and tracing across BFF → Contentful → fund API.

## 11. Demo script

`docs/DEMO.md` captures the exact click-path: create a new `page` in Contentful, attach
`seo`, compose sections, preview as a draft on the live site, publish, observe the page
live at its slug without a deploy, then reorder the homepage's sections and observe the
change.
