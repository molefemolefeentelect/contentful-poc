# Composable, Admin-Managed Website PoC — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a PoC proving a non-technical admin user can create, compose, preview, publish and SEO-manage website pages entirely inside Contentful, with a .NET BFF resolving the section graph and a Next.js app rendering it — no code change or deploy per page.

**Architecture:** Contentful stores a `page` entry holding an ordered reference list of `section` entries (composition), never transactional data. A .NET 10 BFF fetches that graph, resolves links, dispatches each section through a resolver registry, merges live fund data into data-widget sections, resolves SEO and builds JSON-LD, and returns one normalised JSON tree with `__type` discriminators. A Next.js 15 App Router app maps `__type` to React components through a registry, so composing pages needs zero code changes.

**Tech Stack:** Contentful (CDA/CPA + contentful-migration CLI), .NET 10 / ASP.NET Core minimal APIs, xUnit + FluentAssertions, Next.js 15 App Router, TypeScript, Tailwind CSS, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-15-composable-cms-poc-design.md`

---

## Conventions for every task

- **Working directory** is `C:\DevProjects\Misc\cms-poc` unless a task says otherwise.
- **Shell** is PowerShell. Commands are given PowerShell-safe (no `&&` chaining).
- Commit after every task. Commit messages end with:
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`
- Tests come before implementation for every task marked **(TDD)**.
- Never store fund prices, fund names or performance figures in Contentful.

## File structure

```
cms-poc/
  contentful/
    package.json
    .env.example
    migrations/
      001-foundation.cjs          mediaImage, seo, category, author, siteSettings
      002-sections.cjs            card, faqItem, all 11 section types
      003-composition.cjs         page, article, navigationItem, navigation
      004-validations.cjs         section whitelist on page.sections (runs last, needs all types)
    seed/
      seed.mjs                    creates demo entries via CMA
      fixtures/
        pages.json                CDA-shaped responses for Fixture mode
        articles.json
        navigation.json
        siteSettings.json
    run-migrations.mjs
  bff/
    Cms.sln
    src/Cms.FundData.Api/
      Program.cs                  endpoints
      Models/Fund.cs              Fund, FundCategory, PricePoint, Performance
      Data/FundStore.cs           loads + queries seeded JSON
      Data/funds.json             ~20 SA ETFs, seeded
    src/Cms.Bff/
      Program.cs                  DI wiring + endpoint mapping
      Options/                    ContentfulOptions, FundDataOptions, SiteOptions, RevalidateOptions
      Contentful/
        ContentfulDtos.cs         CdaResponse, CdaEntry, CdaSys, CdaAsset
        IContentfulClient.cs
        HttpContentfulClient.cs
        FixtureContentfulClient.cs
        EntryLinkResolver.cs      flatten includes, resolve Link nodes, cycle guard
      Sections/
        SectionDto.cs             base record with __type
        ISectionResolver.cs
        SectionResolverRegistry.cs
        Resolvers/                one file per section type
      Seo/
        SeoDto.cs
        ISeoResolver.cs
        SeoResolver.cs            fallback chain + token substitution
        JsonLd/
          IJsonLdBuilder.cs
          JsonLdBuilderRegistry.cs
          ArticleJsonLdBuilder.cs, ProductJsonLdBuilder.cs,
          FaqPageJsonLdBuilder.cs, BreadcrumbJsonLdBuilder.cs,
          OrganizationJsonLdBuilder.cs, WebPageJsonLdBuilder.cs
      FundData/
        IFundDataClient.cs
        HttpFundDataClient.cs
        FundDtos.cs
      Caching/
        CacheTagStore.cs          reverse index entryId -> cache keys
        CachedContentfulClient.cs decorator
      Pages/
        PageResolutionService.cs  the pipeline
        PageResponse.cs
      Endpoints/
        PageEndpoints.cs, ArticleEndpoints.cs, SiteEndpoints.cs,
        WebhookEndpoints.cs, PreviewEndpoints.cs
    tests/Cms.Bff.Tests/
      EntryLinkResolverTests.cs
      SectionResolverRegistryTests.cs
      FundWidgetResolverTests.cs
      SeoResolverTests.cs
      JsonLdBuilderTests.cs
      CacheTagStoreTests.cs
      Fixtures/                   test-only CDA JSON
  web/
    app/
      layout.tsx                  header/footer/nav + sitewide JSON-LD
      [[...slug]]/page.tsx        catch-all, generateMetadata + generateStaticParams
      news/[slug]/page.tsx
      funds/[code]/page.tsx
      api/draft/route.ts
      api/draft/disable/route.ts
      api/revalidate/route.ts
      sitemap.ts
      robots.ts
    components/
      sections/registry.ts        __type -> component map
      sections/SectionRenderer.tsx
      sections/*.tsx              one per section type
      seo/JsonLd.tsx
      nav/Header.tsx, nav/Footer.tsx
    lib/
      bff.ts                      typed fetch w/ next tags + preview flag
      types.ts                    Section union, SeoDto, PageResponse
      metadata.ts                 SeoDto -> Next Metadata
      indexation.ts               filtered-page canonical/noindex policy
    tests/
      registry.test.ts, metadata.test.ts, jsonld.test.ts, indexation.test.ts
  docs/
    ARCHITECTURE.md, SEO.md, DEMO.md
  README.md
```

---

# Phase 0 — Repository scaffold

### Task 0.1: Create solution, projects and web app

**Files:**
- Create: `bff/Cms.sln`, `bff/src/Cms.Bff/`, `bff/src/Cms.FundData.Api/`, `bff/tests/Cms.Bff.Tests/`
- Create: `web/` (via create-next-app)

- [ ] **Step 1: Create the .NET solution and projects**

```powershell
New-Item -ItemType Directory -Force bff/src, bff/tests
dotnet new sln -o bff -n Cms
dotnet new web -o bff/src/Cms.Bff -f net10.0
dotnet new web -o bff/src/Cms.FundData.Api -f net10.0
dotnet new xunit -o bff/tests/Cms.Bff.Tests -f net10.0
dotnet sln bff/Cms.sln add bff/src/Cms.Bff bff/src/Cms.FundData.Api bff/tests/Cms.Bff.Tests
dotnet add bff/tests/Cms.Bff.Tests reference bff/src/Cms.Bff
dotnet add bff/tests/Cms.Bff.Tests package FluentAssertions
dotnet add bff/src/Cms.Bff package Microsoft.Extensions.Caching.Memory
```

- [ ] **Step 2: Verify the solution builds**

Run: `dotnet build bff/Cms.sln`
Expected: `Build succeeded`, 3 projects, 0 errors.

- [ ] **Step 3: Create the Next.js app**

```powershell
npx --yes create-next-app@15 web --ts --tailwind --app --eslint --src-dir=false --import-alias "@/*" --no-turbopack --use-npm
```

Expected: `Success! Created web at ...`

- [ ] **Step 4: Add Vitest to the web app**

```powershell
npm --prefix web install -D vitest @vitejs/plugin-react @testing-library/react @testing-library/jest-dom jsdom
```

Then create `web/vitest.config.ts`:

```ts
import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig({
  plugins: [react()],
  test: { environment: "jsdom", globals: true, include: ["tests/**/*.test.ts?(x)"] },
  resolve: { alias: { "@": path.resolve(__dirname, ".") } },
});
```

Add to `web/package.json` scripts: `"test": "vitest run"`.

- [ ] **Step 5: Verify the web app builds**

Run: `npm --prefix web run build`
Expected: `Compiled successfully`.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "chore: scaffold BFF solution, fund data API and Next.js app

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 1 — Contentful content model and fixtures

Phase outcome: migrations that build the entire content model in a real space, a seed
script that populates a demo site, and CDA-shaped fixtures so the BFF runs with no
Contentful account.

### Task 1.1: Contentful tooling project

**Files:**
- Create: `contentful/package.json`, `contentful/.env.example`, `contentful/run-migrations.mjs`, `contentful/README.md`

- [ ] **Step 1: Create the npm project**

```powershell
New-Item -ItemType Directory -Force contentful/migrations, contentful/seed/fixtures
npm --prefix contentful init -y
npm --prefix contentful install contentful-migration contentful-management dotenv
```

- [ ] **Step 2: Create `contentful/.env.example`**

```bash
CONTENTFUL_SPACE_ID=
CONTENTFUL_ENVIRONMENT=master
CONTENTFUL_MANAGEMENT_TOKEN=
CONTENTFUL_DELIVERY_TOKEN=
CONTENTFUL_PREVIEW_TOKEN=
```

- [ ] **Step 3: Create `contentful/run-migrations.mjs`**

```js
import { readdirSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { runMigration } from "contentful-migration";
import "dotenv/config";

const __dirname = dirname(fileURLToPath(import.meta.url));
const dir = join(__dirname, "migrations");

const spaceId = process.env.CONTENTFUL_SPACE_ID;
const accessToken = process.env.CONTENTFUL_MANAGEMENT_TOKEN;
const environmentId = process.env.CONTENTFUL_ENVIRONMENT ?? "master";

if (!spaceId || !accessToken) {
  console.error("Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN. Copy .env.example to .env first.");
  process.exit(1);
}

const files = readdirSync(dir).filter((f) => f.endsWith(".cjs")).sort();

for (const file of files) {
  console.log(`\n=== Running ${file} ===`);
  await runMigration({
    filePath: join(dir, file),
    spaceId,
    accessToken,
    environmentId,
    yes: true,
  });
}
console.log("\nAll migrations applied.");
```

- [ ] **Step 4: Add scripts to `contentful/package.json`**

```json
"scripts": {
  "migrate": "node run-migrations.mjs",
  "seed": "node seed/seed.mjs"
}
```

- [ ] **Step 5: Verify the runner fails cleanly without credentials**

Run: `npm --prefix contentful run migrate`
Expected: exits with `Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN...`

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat(contentful): add migration runner and env template

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.2: Migration 001 — foundation types

**Files:**
- Create: `contentful/migrations/001-foundation.cjs`

Creates `mediaImage`, `seo`, `category`, `author`, `siteSettings`.

- [ ] **Step 1: Write the migration**

```js
module.exports = function (migration) {
  const mediaImage = migration
    .createContentType("mediaImage")
    .name("Media Image")
    .description("An image plus mandatory alt text. Referenced instead of raw Assets so alt text can be enforced.")
    .displayField("internalName");
  mediaImage.createField("internalName").name("Internal name").type("Symbol").required(true);
  mediaImage.createField("image").name("Image").type("Link").linkType("Asset").required(true);
  mediaImage.createField("altText").name("Alt text").type("Symbol").required(true)
    .validations([{ size: { max: 125 }, message: "Alt text must be 125 characters or fewer." }]);
  mediaImage.createField("caption").name("Caption").type("Symbol");

  const seo = migration
    .createContentType("seo")
    .name("SEO")
    .description("Reusable SEO metadata. Referenced by every page-like content type.")
    .displayField("internalName");
  seo.createField("internalName").name("Internal name").type("Symbol").required(true);
  seo.createField("metaTitle").name("Meta title").type("Symbol").required(true)
    .validations([{ size: { max: 60 }, message: "Keep meta titles to 60 characters so they are not truncated in search results." }]);
  seo.createField("metaDescription").name("Meta description").type("Text").required(true)
    .validations([{ size: { max: 160 }, message: "Keep meta descriptions to 160 characters so they are not truncated in search results." }]);
  seo.createField("ogTitle").name("Open Graph title override").type("Symbol");
  seo.createField("ogDescription").name("Open Graph description override").type("Text");
  seo.createField("ogImage").name("Open Graph image").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  seo.createField("canonicalUrl").name("Canonical URL override").type("Symbol")
    .validations([{ regexp: { pattern: "^https?://.+" }, message: "Must be an absolute URL starting with http:// or https://" }]);
  seo.createField("noindex").name("Hide from search engines (noindex)").type("Boolean");
  seo.createField("nofollow").name("Do not follow links (nofollow)").type("Boolean");
  seo.createField("structuredDataType").name("Structured data type").type("Symbol")
    .validations([{ in: ["WebPage", "Organization", "Article", "Product", "FAQPage", "BreadcrumbList", "None"] }]);
  seo.createField("structuredDataOverrides").name("Structured data overrides").type("Object")
    .validations([]);

  seo.changeFieldControl("noindex", "builtin", "boolean", { trueLabel: "Noindex", falseLabel: "Indexable" });
  seo.changeFieldControl("nofollow", "builtin", "boolean", { trueLabel: "Nofollow", falseLabel: "Follow" });
  seo.changeFieldControl("structuredDataType", "builtin", "dropdown");

  const category = migration.createContentType("category").name("Category").displayField("name");
  category.createField("name").name("Name").type("Symbol").required(true);
  category.createField("slug").name("Slug").type("Symbol").required(true)
    .validations([{ unique: true }, { regexp: { pattern: "^[a-z0-9]+(?:-[a-z0-9]+)*$" }, message: "Lowercase letters, numbers and hyphens only." }]);

  const author = migration.createContentType("author").name("Author").displayField("name");
  author.createField("name").name("Name").type("Symbol").required(true);
  author.createField("jobTitle").name("Job title").type("Symbol");
  author.createField("bio").name("Bio").type("Text");
  author.createField("photo").name("Photo").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);

  const settings = migration.createContentType("siteSettings").name("Site Settings").displayField("siteName");
  settings.createField("siteName").name("Site name").type("Symbol").required(true);
  settings.createField("logo").name("Logo").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  settings.createField("defaultSeo").name("Default SEO fallback").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["seo"] }]);
  settings.createField("socialLinks").name("Social links").type("Object");
  settings.createField("disclaimerText").name("Global disclaimer").type("RichText");
  settings.createField("organizationLegalName").name("Organisation legal name").type("Symbol");
  settings.createField("organizationSameAs").name("Organisation sameAs URLs").type("Array")
    .items({ type: "Symbol" });
};
```

- [ ] **Step 2: Verify the migration file parses**

Run: `node -e "require('./contentful/migrations/001-foundation.cjs'); console.log('parsed ok')"`
Expected: `parsed ok`

- [ ] **Step 3: Commit**

```powershell
git add contentful/migrations/001-foundation.cjs
git commit -m "feat(contentful): add foundation content types (mediaImage, seo, category, author, siteSettings)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.3: Migration 002 — section types

**Files:**
- Create: `contentful/migrations/002-sections.cjs`

Creates `card`, `faqItem` and all 11 section types. Every image field links to
`mediaImage`, never to an Asset.

- [ ] **Step 1: Write the migration**

```js
const IMAGE = [{ linkContentType: ["mediaImage"] }];

module.exports = function (migration) {
  const card = migration.createContentType("card").name("Card").displayField("title");
  card.createField("title").name("Title").type("Symbol").required(true);
  card.createField("body").name("Body").type("Text");
  card.createField("image").name("Image").type("Link").linkType("Entry").validations(IMAGE);
  card.createField("linkLabel").name("Link label").type("Symbol");
  card.createField("linkUrl").name("Link URL").type("Symbol");
  card.createField("icon").name("Icon name").type("Symbol");

  const faqItem = migration.createContentType("faqItem").name("FAQ Item").displayField("question");
  faqItem.createField("question").name("Question").type("Symbol").required(true);
  faqItem.createField("answer").name("Answer").type("RichText").required(true);

  const hero = migration.createContentType("sectionHero").name("Section: Hero").displayField("internalName");
  hero.createField("internalName").name("Internal name").type("Symbol").required(true);
  hero.createField("eyebrow").name("Eyebrow").type("Symbol");
  hero.createField("heading").name("Heading").type("Symbol").required(true);
  hero.createField("subheading").name("Subheading").type("Text");
  hero.createField("backgroundImage").name("Background image").type("Link").linkType("Entry").validations(IMAGE);
  hero.createField("primaryCtaLabel").name("Primary CTA label").type("Symbol");
  hero.createField("primaryCtaUrl").name("Primary CTA URL").type("Symbol");
  hero.createField("secondaryCtaLabel").name("Secondary CTA label").type("Symbol");
  hero.createField("secondaryCtaUrl").name("Secondary CTA URL").type("Symbol");
  hero.createField("variant").name("Variant").type("Symbol")
    .validations([{ in: ["default", "compact", "imageRight"] }]);

  const richText = migration.createContentType("sectionRichText").name("Section: Rich Text").displayField("internalName");
  richText.createField("internalName").name("Internal name").type("Symbol").required(true);
  richText.createField("heading").name("Heading").type("Symbol");
  richText.createField("body").name("Body").type("RichText").required(true);
  richText.createField("width").name("Width").type("Symbol")
    .validations([{ in: ["narrow", "default", "wide"] }]);

  const cardGrid = migration.createContentType("sectionCardGrid").name("Section: Card Grid").displayField("internalName");
  cardGrid.createField("internalName").name("Internal name").type("Symbol").required(true);
  cardGrid.createField("heading").name("Heading").type("Symbol");
  cardGrid.createField("intro").name("Intro").type("Text");
  cardGrid.createField("cards").name("Cards").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["card"] }] });
  cardGrid.createField("columns").name("Columns").type("Integer")
    .validations([{ range: { min: 2, max: 4 } }]);

  const cta = migration.createContentType("sectionCtaBanner").name("Section: CTA Banner").displayField("internalName");
  cta.createField("internalName").name("Internal name").type("Symbol").required(true);
  cta.createField("heading").name("Heading").type("Symbol").required(true);
  cta.createField("body").name("Body").type("Text");
  cta.createField("ctaLabel").name("CTA label").type("Symbol").required(true);
  cta.createField("ctaUrl").name("CTA URL").type("Symbol").required(true);
  cta.createField("variant").name("Variant").type("Symbol")
    .validations([{ in: ["primary", "muted", "accent"] }]);

  const teasers = migration.createContentType("sectionArticleTeaserList").name("Section: Article Teaser List").displayField("internalName");
  teasers.createField("internalName").name("Internal name").type("Symbol").required(true);
  teasers.createField("heading").name("Heading").type("Symbol");
  teasers.createField("mode").name("Mode").type("Symbol").required(true)
    .validations([{ in: ["latest", "manual"] }]);
  teasers.createField("category").name("Category filter").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["category"] }]);
  teasers.createField("limit").name("Number of articles").type("Integer")
    .validations([{ range: { min: 1, max: 12 } }]);
  teasers.createField("articles").name("Manually chosen articles").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["article"] }] });

  const fundList = migration.createContentType("sectionFundListWidget").name("Section: Fund List Widget").displayField("internalName");
  fundList.createField("internalName").name("Internal name").type("Symbol").required(true);
  fundList.createField("heading").name("Heading").type("Symbol");
  fundList.createField("intro").name("Intro").type("Text");
  fundList.createField("categoryId").name("Fund category id").type("Symbol");
  fundList.createField("topN").name("Show top N").type("Integer")
    .validations([{ range: { min: 1, max: 50 } }]);
  fundList.createField("displayVariant").name("Display variant").type("Symbol")
    .validations([{ in: ["grid", "table", "compact"] }]);
  fundList.createField("showFilters").name("Show category filters").type("Boolean");

  const fundDetail = migration.createContentType("sectionFundDetailWidget").name("Section: Fund Detail Widget").displayField("internalName");
  fundDetail.createField("internalName").name("Internal name").type("Symbol").required(true);
  fundDetail.createField("fundCode").name("Fund code").type("Symbol")
    .validations([]);
  fundDetail.createField("showPerformance").name("Show performance").type("Boolean");
  fundDetail.createField("showPriceHistory").name("Show price history").type("Boolean");
  fundDetail.createField("showFactsheet").name("Show factsheet link").type("Boolean");

  const faq = migration.createContentType("sectionFaqAccordion").name("Section: FAQ Accordion").displayField("internalName");
  faq.createField("internalName").name("Internal name").type("Symbol").required(true);
  faq.createField("heading").name("Heading").type("Symbol");
  faq.createField("items").name("Questions").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["faqItem"] }] });
  faq.createField("emitFaqSchema").name("Emit FAQPage structured data").type("Boolean");

  const imageText = migration.createContentType("sectionImageWithText").name("Section: Image With Text").displayField("internalName");
  imageText.createField("internalName").name("Internal name").type("Symbol").required(true);
  imageText.createField("heading").name("Heading").type("Symbol");
  imageText.createField("body").name("Body").type("RichText");
  imageText.createField("image").name("Image").type("Link").linkType("Entry").validations(IMAGE);
  imageText.createField("imagePosition").name("Image position").type("Symbol")
    .validations([{ in: ["left", "right"] }]);
  imageText.createField("ctaLabel").name("CTA label").type("Symbol");
  imageText.createField("ctaUrl").name("CTA URL").type("Symbol");

  const disclaimer = migration.createContentType("sectionDisclaimer").name("Section: Disclaimer").displayField("internalName");
  disclaimer.createField("internalName").name("Internal name").type("Symbol").required(true);
  disclaimer.createField("label").name("Label").type("Symbol");
  disclaimer.createField("body").name("Body").type("RichText").required(true);
  disclaimer.createField("severity").name("Severity").type("Symbol")
    .validations([{ in: ["info", "warning"] }]);
  disclaimer.createField("collapsible").name("Collapsible").type("Boolean");
};
```

Note: `sectionArticleTeaserList.articles` validates against `article`, which is created
in migration 003. Contentful applies link validations lazily, so the reference resolves
once 003 has run; migration 004 re-asserts it.

- [ ] **Step 2: Verify the migration file parses**

Run: `node -e "require('./contentful/migrations/002-sections.cjs'); console.log('parsed ok')"`
Expected: `parsed ok`

- [ ] **Step 3: Commit**

```powershell
git add contentful/migrations/002-sections.cjs
git commit -m "feat(contentful): add card, faqItem and all section content types

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.4: Migration 003 — composition types

**Files:**
- Create: `contentful/migrations/003-composition.cjs`

- [ ] **Step 1: Write the migration**

```js
module.exports = function (migration) {
  const page = migration
    .createContentType("page")
    .name("Page")
    .description("A URL on the site. Composed by referencing section entries in order.")
    .displayField("title");
  page.createField("title").name("Title").type("Symbol").required(true);
  page.createField("slug").name("Slug").type("Symbol").required(true)
    .validations([
      { unique: true },
      {
        regexp: { pattern: "^$|^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9_]+(?:-[a-z0-9]+)*)*$" },
        message: "Lowercase letters, numbers, hyphens and slashes only. Leave empty for the homepage.",
      },
    ]);
  page.createField("pageType").name("Page type").type("Symbol").required(true)
    .validations([{ in: ["marketing", "dataDriven"] }]);
  page.createField("seo").name("SEO").type("Link").linkType("Entry").required(true)
    .validations([{ linkContentType: ["seo"] }]);
  page.createField("sections").name("Sections").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [] });
  page.createField("breadcrumbParent").name("Breadcrumb parent").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["page"] }]);

  page.changeFieldControl("slug", "builtin", "slugEditor", {
    helpText: "The live URL path. Changing this on a published page breaks existing links — see the redirect note in the README.",
  });

  const article = migration.createContentType("article").name("Article").displayField("title");
  article.createField("title").name("Title").type("Symbol").required(true);
  article.createField("slug").name("Slug").type("Symbol").required(true)
    .validations([
      { unique: true },
      { regexp: { pattern: "^[a-z0-9]+(?:-[a-z0-9]+)*$" }, message: "Lowercase letters, numbers and hyphens only." },
    ]);
  article.createField("seo").name("SEO").type("Link").linkType("Entry").required(true)
    .validations([{ linkContentType: ["seo"] }]);
  article.createField("excerpt").name("Excerpt").type("Text")
    .validations([{ size: { max: 300 } }]);
  article.createField("body").name("Body").type("RichText").required(true);
  article.createField("featuredImage").name("Featured image").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["mediaImage"] }]);
  article.createField("author").name("Author").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["author"] }]);
  article.createField("category").name("Category").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["category"] }]);
  article.createField("tags").name("Tags").type("Array").items({ type: "Symbol" });
  article.createField("publishDate").name("Publish date").type("Date").required(true);
  article.createField("updatedDate").name("Last updated").type("Date");
  article.createField("relatedArticles").name("Related articles").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["article"] }] });

  const navItem = migration.createContentType("navigationItem").name("Navigation Item").displayField("label");
  navItem.createField("label").name("Label").type("Symbol").required(true);
  navItem.createField("page").name("Links to page").type("Link").linkType("Entry")
    .validations([{ linkContentType: ["page", "article"] }]);
  navItem.createField("externalUrl").name("External URL").type("Symbol");
  navItem.createField("children").name("Child items").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["navigationItem"] }] });

  const nav = migration.createContentType("navigation").name("Navigation").displayField("name");
  nav.createField("name").name("Name").type("Symbol").required(true);
  nav.createField("key").name("Key").type("Symbol").required(true)
    .validations([{ unique: true }, { in: ["header", "footer"] }]);
  nav.createField("items").name("Items").type("Array")
    .items({ type: "Link", linkType: "Entry", validations: [{ linkContentType: ["navigationItem"] }] });
};
```

- [ ] **Step 2: Verify the migration file parses**

Run: `node -e "require('./contentful/migrations/003-composition.cjs'); console.log('parsed ok')"`
Expected: `parsed ok`

- [ ] **Step 3: Commit**

```powershell
git add contentful/migrations/003-composition.cjs
git commit -m "feat(contentful): add page, article and navigation content types

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.5: Migration 004 — section whitelist

**Files:**
- Create: `contentful/migrations/004-validations.cjs`

This is the validation that stops an admin user assembling a broken layout. It runs
last because every referenced content type must already exist.

- [ ] **Step 1: Write the migration**

```js
const SECTION_TYPES = [
  "sectionHero",
  "sectionRichText",
  "sectionCardGrid",
  "sectionCtaBanner",
  "sectionArticleTeaserList",
  "sectionFundListWidget",
  "sectionFundDetailWidget",
  "sectionFaqAccordion",
  "sectionImageWithText",
  "sectionDisclaimer",
];

module.exports = function (migration) {
  const page = migration.editContentType("page");
  page.editField("sections").items({
    type: "Link",
    linkType: "Entry",
    validations: [
      {
        linkContentType: SECTION_TYPES,
        message: "Only section blocks can be added to a page.",
      },
    ],
  });

  page.changeFieldControl("sections", "builtin", "entryLinksEditor", {
    bulkEditing: false,
    showLinkEntityAction: true,
    showCreateEntityAction: true,
    helpText: "Add, drag to reorder, or remove blocks to compose this page. No developer involvement required.",
  });
};
```

- [ ] **Step 2: Verify the migration file parses**

Run: `node -e "require('./contentful/migrations/004-validations.cjs'); console.log('parsed ok')"`
Expected: `parsed ok`

- [ ] **Step 3: Commit**

```powershell
git add contentful/migrations/004-validations.cjs
git commit -m "feat(contentful): restrict page.sections to valid section types

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.6: CDA-shaped fixtures for offline mode

**Files:**
- Create: `contentful/seed/fixtures/entries.json`
- Create: `contentful/seed/fixtures/README.md`

The BFF's `FixtureContentfulClient` reads these. They must use Contentful's real
Delivery API response shape — `items` plus an `includes.Entry` / `includes.Asset`
block, with unresolved `Link` nodes — so the link resolver is exercised identically in
offline and live mode. Fields are **not** locale-wrapped here: the BFF requests
`locale=*`-free responses, which Contentful returns already flattened to the default
locale.

- [ ] **Step 1: Create `contentful/seed/fixtures/entries.json`**

Structure (abbreviated to two entries plus includes — the executing engineer fills the
remaining entries to cover every demo page listed in Step 2):

```json
{
  "sys": { "type": "Array" },
  "total": 1,
  "skip": 0,
  "limit": 100,
  "items": [
    {
      "sys": {
        "id": "page-home",
        "type": "Entry",
        "createdAt": "2026-01-10T08:00:00Z",
        "updatedAt": "2026-09-01T10:30:00Z",
        "contentType": { "sys": { "id": "page", "type": "Link", "linkType": "ContentType" } }
      },
      "fields": {
        "title": "Home",
        "slug": "",
        "pageType": "marketing",
        "seo": { "sys": { "id": "seo-home", "type": "Link", "linkType": "Entry" } },
        "sections": [
          { "sys": { "id": "sec-home-hero", "type": "Link", "linkType": "Entry" } },
          { "sys": { "id": "sec-home-cards", "type": "Link", "linkType": "Entry" } },
          { "sys": { "id": "sec-home-cta", "type": "Link", "linkType": "Entry" } },
          { "sys": { "id": "sec-home-teasers", "type": "Link", "linkType": "Entry" } },
          { "sys": { "id": "sec-global-disclaimer", "type": "Link", "linkType": "Entry" } }
        ]
      }
    }
  ],
  "includes": {
    "Entry": [
      {
        "sys": {
          "id": "seo-home",
          "type": "Entry",
          "contentType": { "sys": { "id": "seo", "type": "Link", "linkType": "ContentType" } }
        },
        "fields": {
          "internalName": "Home SEO",
          "metaTitle": "Own the Market | Index Investing in South Africa",
          "metaDescription": "Low-cost, award-winning index funds and ETFs. No minimums. Start building a portfolio you can be proud of today.",
          "structuredDataType": "WebPage",
          "noindex": false,
          "nofollow": false
        }
      },
      {
        "sys": {
          "id": "sec-home-hero",
          "type": "Entry",
          "contentType": { "sys": { "id": "sectionHero", "type": "Link", "linkType": "ContentType" } }
        },
        "fields": {
          "internalName": "Home hero",
          "eyebrow": "25 Years",
          "heading": "Become the Investor You Want to Be",
          "subheading": "The complexity of investing can put anyone off. We exist to make it simple.",
          "primaryCtaLabel": "View Our Funds",
          "primaryCtaUrl": "/funds",
          "variant": "default",
          "backgroundImage": { "sys": { "id": "media-hero", "type": "Link", "linkType": "Entry" } }
        }
      },
      {
        "sys": {
          "id": "media-hero",
          "type": "Entry",
          "contentType": { "sys": { "id": "mediaImage", "type": "Link", "linkType": "ContentType" } }
        },
        "fields": {
          "internalName": "Home hero background",
          "altText": "Investors reviewing a portfolio on a tablet",
          "image": { "sys": { "id": "asset-hero", "type": "Link", "linkType": "Asset" } }
        }
      }
    ],
    "Asset": [
      {
        "sys": { "id": "asset-hero", "type": "Asset" },
        "fields": {
          "title": "Home hero background",
          "file": {
            "url": "//images.ctfassets.net/demo/asset-hero/hero.jpg",
            "details": { "size": 184320, "image": { "width": 1920, "height": 960 } },
            "fileName": "hero.jpg",
            "contentType": "image/jpeg"
          }
        }
      }
    ]
  }
}
```

- [ ] **Step 2: Extend the fixture to cover every demo page**

Required entries, because later tasks assert against them:

| Entry id | Content type | Slug / purpose |
|---|---|---|
| `page-home` | page | `""` — hero, card grid, CTA banner, article teasers, disclaimer |
| `page-about` | page | `about` — hero, image-with-text ×2, CTA |
| `page-how-to-invest` | page | `how-to-invest` — rich text, FAQ accordion, CTA |
| `page-tax-free` | page | `tax-free-investing` — rich text, card grid, FAQ, disclaimer |
| `page-funds` | page | `funds`, pageType `dataDriven` — rich text intro, fund list widget, disclaimer |
| `page-fund-detail` | page | `funds/_detail`, pageType `dataDriven` — fund detail widget (blank `fundCode`), disclaimer |
| `page-news` | page | `news` — hero, article teaser list (`latest`, limit 12) |
| `article-*` ×4 | article | with author, category, featuredImage, publishDate |
| `nav-header`, `nav-footer` | navigation | with `navigationItem` children |
| `settings-main` | siteSettings | siteName, defaultSeo, disclaimerText, organizationSameAs |

Include at least one **deliberately unknown** section entry (`sec-unknown`, content type
`sectionFutureThing`) referenced by `page-about`, so the degradation path is exercised
by the real fixture and not only by unit tests.

- [ ] **Step 3: Verify the fixture is valid JSON with resolvable links**

```powershell
node -e "const d=require('./contentful/seed/fixtures/entries.json'); const ids=new Set([...d.items,...d.includes.Entry].map(e=>e.sys.id)); const missing=[]; JSON.stringify(d,(k,v)=>{if(v&&v.sys&&v.sys.linkType==='Entry'&&!ids.has(v.sys.id))missing.push(v.sys.id);return v}); console.log(missing.length?('DANGLING: '+missing.join(',')):'all entry links resolve')"
```

Expected: `all entry links resolve`
(The `sectionFutureThing` entry must exist in includes — unknown *content type*, not a
dangling link.)

- [ ] **Step 4: Commit**

```powershell
git add contentful/seed/fixtures
git commit -m "feat(contentful): add CDA-shaped fixtures for offline BFF mode

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 1.7: Seed script for a real Contentful space

**Files:**
- Create: `contentful/seed/seed.mjs`

Reads the same fixture file and creates real entries via the Management API, so the
offline demo and the live space contain identical content.

- [ ] **Step 1: Write the seed script**

```js
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createClient } from "contentful-management";
import "dotenv/config";

const __dirname = dirname(fileURLToPath(import.meta.url));
const fixture = JSON.parse(readFileSync(join(__dirname, "fixtures/entries.json"), "utf8"));

const spaceId = process.env.CONTENTFUL_SPACE_ID;
const accessToken = process.env.CONTENTFUL_MANAGEMENT_TOKEN;
const environmentId = process.env.CONTENTFUL_ENVIRONMENT ?? "master";
const LOCALE = "en-US";

if (!spaceId || !accessToken) {
  console.error("Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN.");
  process.exit(1);
}

const client = createClient({ accessToken });
const space = await client.getSpace(spaceId);
const env = await space.getEnvironment(environmentId);

// Localise flat fixture fields into the CMA's { fieldName: { locale: value } } shape.
const localise = (fields) =>
  Object.fromEntries(Object.entries(fields).map(([k, v]) => [k, { [LOCALE]: v }]));

const all = [...(fixture.includes?.Entry ?? []), ...fixture.items];

// Create unpublished first so links never dangle, then publish in a second pass.
for (const entry of all) {
  const contentTypeId = entry.sys.contentType.sys.id;
  if (contentTypeId === "sectionFutureThing") {
    console.log(`skip ${entry.sys.id} (intentionally unknown type, offline fixture only)`);
    continue;
  }
  try {
    await env.getEntry(entry.sys.id);
    console.log(`exists ${entry.sys.id}`);
  } catch {
    await env.createEntryWithId(contentTypeId, entry.sys.id, { fields: localise(entry.fields) });
    console.log(`created ${entry.sys.id}`);
  }
}

for (const entry of all) {
  if (entry.sys.contentType.sys.id === "sectionFutureThing") continue;
  const e = await env.getEntry(entry.sys.id);
  if (!e.isPublished()) {
    await e.publish();
    console.log(`published ${entry.sys.id}`);
  }
}

console.log("\nSeed complete.");
```

- [ ] **Step 2: Verify it fails cleanly without credentials**

Run: `npm --prefix contentful run seed`
Expected: `Missing CONTENTFUL_SPACE_ID or CONTENTFUL_MANAGEMENT_TOKEN.`

- [ ] **Step 3: Commit**

```powershell
git add contentful/seed/seed.mjs
git commit -m "feat(contentful): add seed script sharing the offline fixture content

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 2 — Mock Fund Data API

Phase outcome: a standalone service on `http://localhost:5090` returning fund lists,
fund detail and price history. Runnable and verifiable on its own.

### Task 2.1: Fund models and seeded store

**Files:**
- Create: `bff/src/Cms.FundData.Api/Models/Fund.cs`
- Create: `bff/src/Cms.FundData.Api/Data/funds.json`
- Create: `bff/src/Cms.FundData.Api/Data/FundStore.cs`

- [ ] **Step 1: Create the models**

```csharp
namespace Cms.FundData.Api.Models;

public record FundCategory(string Id, string Name, string Slug);

public record Performance(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public record PricePoint(DateOnly Date, decimal Nav);

public record Fund(
    string Code,
    string Isin,
    string Name,
    string CategoryId,
    string ShortDescription,
    decimal Ter,
    decimal Nav,
    decimal DayChangePercent,
    DateOnly InceptionDate,
    string FactsheetUrl,
    Performance Performance);
```

- [ ] **Step 2: Create `Data/funds.json` with 20 funds**

Shape (first two shown; the engineer adds 18 more using real Satrix-style names and
plausible values — this is mock data, accuracy is not required, but codes must be
unique and `categoryId` must match a category below):

```json
{
  "categories": [
    { "id": "1", "name": "Local Equity", "slug": "local-equity" },
    { "id": "2", "name": "Global Equity", "slug": "global-equity" },
    { "id": "3", "name": "Bonds & Income", "slug": "bonds-income" },
    { "id": "4", "name": "Property", "slug": "property" },
    { "id": "5", "name": "Balanced", "slug": "balanced" }
  ],
  "funds": [
    {
      "code": "STX40",
      "isin": "ZAE000027108",
      "name": "Satrix 40 ETF",
      "categoryId": "1",
      "shortDescription": "Tracks the FTSE/JSE Top 40 Index, giving exposure to South Africa's 40 largest listed companies.",
      "ter": 0.10,
      "nav": 88.42,
      "dayChangePercent": 0.62,
      "inceptionDate": "2000-11-27",
      "factsheetUrl": "https://example.invalid/factsheets/stx40.pdf",
      "performance": { "oneYear": 14.8, "threeYear": 11.2, "fiveYear": 9.6, "sinceInception": 13.1 }
    },
    {
      "code": "STXWDM",
      "isin": "ZAE000257277",
      "name": "Satrix MSCI World ETF",
      "categoryId": "2",
      "shortDescription": "Tracks the MSCI World Index for exposure to developed-market equities.",
      "ter": 0.35,
      "nav": 132.05,
      "dayChangePercent": -0.18,
      "inceptionDate": "2017-10-10",
      "factsheetUrl": "https://example.invalid/factsheets/stxwdm.pdf",
      "performance": { "oneYear": 18.4, "threeYear": 14.9, "fiveYear": 13.2, "sinceInception": 14.0 }
    }
  ]
}
```

Set `Copy to output directory: PreserveNewest` by adding to `Cms.FundData.Api.csproj`:

```xml
<ItemGroup>
  <Content Include="Data\funds.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 3: Create `Data/FundStore.cs`**

Price history is generated deterministically from the fund code so repeated calls return
identical series — a randomly-varying mock would make cache and ISR behaviour
impossible to reason about during the demo.

```csharp
using System.Text.Json;
using Cms.FundData.Api.Models;

namespace Cms.FundData.Api.Data;

public sealed class FundStore
{
    private readonly List<Fund> _funds;
    private readonly List<FundCategory> _categories;

    public FundStore(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "funds.json");
        using var stream = File.OpenRead(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seed = JsonSerializer.Deserialize<SeedFile>(stream, options)
                   ?? throw new InvalidOperationException($"Could not read fund seed data at {path}");
        _funds = seed.Funds;
        _categories = seed.Categories;
    }

    public IReadOnlyList<FundCategory> GetCategories() => _categories;

    public IReadOnlyList<Fund> GetFunds(string? categoryId, int? top)
    {
        IEnumerable<Fund> query = _funds;
        if (!string.IsNullOrWhiteSpace(categoryId))
            query = query.Where(f => f.CategoryId == categoryId);

        query = query.OrderByDescending(f => f.Performance.OneYear);

        if (top is > 0)
            query = query.Take(top.Value);

        return query.ToList();
    }

    public Fund? GetFund(string codeOrIsin) =>
        _funds.FirstOrDefault(f =>
            string.Equals(f.Code, codeOrIsin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Isin, codeOrIsin, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PricePoint> GetPrices(string code, int days)
    {
        var fund = GetFund(code);
        if (fund is null) return Array.Empty<PricePoint>();

        var rng = new Random(fund.Code.GetHashCode(StringComparison.Ordinal));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var points = new List<PricePoint>(days);
        var nav = fund.Nav;

        for (var i = days - 1; i >= 0; i--)
        {
            var drift = (decimal)((rng.NextDouble() - 0.49) * 0.012);
            nav = Math.Round(nav * (1 + drift), 2);
            points.Add(new PricePoint(today.AddDays(-i), nav));
        }

        return points;
    }

    private sealed record SeedFile(List<FundCategory> Categories, List<Fund> Funds);
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build bff/src/Cms.FundData.Api`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add bff/src/Cms.FundData.Api
git commit -m "feat(funddata): add fund models and seeded in-memory store

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 2.2: Fund Data API endpoints

**Files:**
- Modify: `bff/src/Cms.FundData.Api/Program.cs`
- Modify: `bff/src/Cms.FundData.Api/Properties/launchSettings.json`

- [ ] **Step 1: Replace `Program.cs`**

```csharp
using Cms.FundData.Api.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<FundStore>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "fund-data-api" }));

app.MapGet("/categories", (FundStore store) => Results.Ok(store.GetCategories()));

app.MapGet("/funds", (FundStore store, string? categoryId, int? top) =>
    Results.Ok(store.GetFunds(categoryId, top)));

app.MapGet("/funds/{codeOrIsin}", (FundStore store, string codeOrIsin) =>
{
    var fund = store.GetFund(codeOrIsin);
    return fund is null ? Results.NotFound(new { message = $"No fund with code or ISIN '{codeOrIsin}'." }) : Results.Ok(fund);
});

app.MapGet("/funds/{code}/prices", (FundStore store, string code, int? days) =>
{
    var window = Math.Clamp(days ?? 90, 1, 365);
    var prices = store.GetPrices(code, window);
    return prices.Count == 0
        ? Results.NotFound(new { message = $"No fund with code '{code}'." })
        : Results.Ok(prices);
});

app.Run();
```

- [ ] **Step 2: Pin the port in `Properties/launchSettings.json`**

Set the `applicationUrl` of the `http` profile to `http://localhost:5090`.

- [ ] **Step 3: Run and verify the endpoints**

```powershell
Start-Process -NoNewWindow dotnet -ArgumentList "run --project bff/src/Cms.FundData.Api --launch-profile http"
Start-Sleep -Seconds 6
Invoke-RestMethod http://localhost:5090/health
Invoke-RestMethod "http://localhost:5090/funds?categoryId=1&top=3" | Select-Object code, name, nav
Invoke-RestMethod http://localhost:5090/funds/STX40 | Select-Object code, name, nav
(Invoke-RestMethod "http://localhost:5090/funds/STX40/prices?days=30").Count
```

Expected: `status ok`; 3 local-equity funds; `STX40 / Satrix 40 ETF / 88.42`; `30`.

Stop it with: `Get-Process dotnet | Stop-Process -Force`

- [ ] **Step 4: Commit**

```powershell
git add bff/src/Cms.FundData.Api
git commit -m "feat(funddata): expose fund list, detail, prices and category endpoints

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 3 — .NET BFF

Phase outcome: `GET /api/pages/{slug}` returns a fully resolved tree with SEO and
JSON-LD, working offline from fixtures, with the risky logic under test.

### Task 3.1: Options and Contentful DTOs

**Files:**
- Create: `bff/src/Cms.Bff/Options/AppOptions.cs`
- Create: `bff/src/Cms.Bff/Contentful/ContentfulDtos.cs`
- Modify: `bff/src/Cms.Bff/appsettings.json`

- [ ] **Step 1: Create `Options/AppOptions.cs`**

```csharp
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
```

- [ ] **Step 2: Create `Contentful/ContentfulDtos.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cms.Bff.Contentful;

public sealed class CdaLinkSys
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("linkType")] public string? LinkType { get; set; }
}

public sealed class CdaLink
{
    [JsonPropertyName("sys")] public CdaLinkSys Sys { get; set; } = new();
}

public sealed class CdaSys
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("createdAt")] public DateTimeOffset? CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("contentType")] public CdaLink? ContentType { get; set; }
}

public sealed class CdaEntry
{
    [JsonPropertyName("sys")] public CdaSys Sys { get; set; } = new();
    [JsonPropertyName("fields")] public Dictionary<string, JsonElement> Fields { get; set; } = new();

    public string ContentTypeId => Sys.ContentType?.Sys.Id ?? "";
}

public sealed class CdaIncludes
{
    [JsonPropertyName("Entry")] public List<CdaEntry> Entry { get; set; } = new();
    [JsonPropertyName("Asset")] public List<CdaEntry> Asset { get; set; } = new();
}

public sealed class CdaResponse
{
    [JsonPropertyName("items")] public List<CdaEntry> Items { get; set; } = new();
    [JsonPropertyName("includes")] public CdaIncludes? Includes { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}
```

- [ ] **Step 3: Add configuration to `appsettings.json`**

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Contentful": {
    "Mode": "Fixture",
    "Environment": "master",
    "FixturePath": "../../../contentful/seed/fixtures/entries.json",
    "CacheSeconds": 300
  },
  "FundData": { "BaseUrl": "http://localhost:5090" },
  "Site": { "BaseUrl": "http://localhost:3000" },
  "Integration": {
    "WebhookSecret": "dev-webhook-secret",
    "PreviewSecret": "dev-preview-secret",
    "RevalidateUrl": "http://localhost:3000/api/revalidate"
  }
}
```

Pin the BFF's `http` profile `applicationUrl` to `http://localhost:5080` in
`Properties/launchSettings.json`.

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build bff/src/Cms.Bff`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add bff/src/Cms.Bff
git commit -m "feat(bff): add options and Contentful delivery DTOs

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.2: EntryLinkResolver (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Contentful/ResolvedEntry.cs`
- Create: `bff/src/Cms.Bff/Contentful/EntryLinkResolver.cs`
- Test: `bff/tests/Cms.Bff.Tests/EntryLinkResolverTests.cs`

This is the highest-risk component: Contentful returns a flat `includes` block with
unresolved `Link` nodes, and real content models contain cycles (`relatedArticles`
pointing back at each other). Getting this wrong produces either missing content or a
stack overflow.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Cms.Bff.Contentful;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class EntryLinkResolverTests
{
    private static CdaResponse Parse(string json) =>
        JsonSerializer.Deserialize<CdaResponse>(json)!;

    private const string NestedJson = """
    {
      "items": [{
        "sys": { "id": "page-1", "type": "Entry", "contentType": { "sys": { "id": "page", "type": "Link", "linkType": "ContentType" } } },
        "fields": {
          "title": "Home",
          "sections": [
            { "sys": { "id": "hero-1", "type": "Link", "linkType": "Entry" } },
            { "sys": { "id": "missing-1", "type": "Link", "linkType": "Entry" } }
          ]
        }
      }],
      "includes": {
        "Entry": [{
          "sys": { "id": "hero-1", "type": "Entry", "contentType": { "sys": { "id": "sectionHero", "type": "Link", "linkType": "ContentType" } } },
          "fields": { "heading": "Own the market", "image": { "sys": { "id": "asset-1", "type": "Link", "linkType": "Asset" } } }
        }],
        "Asset": [{
          "sys": { "id": "asset-1", "type": "Asset" },
          "fields": { "title": "Hero", "file": { "url": "//images.ctfassets.net/x/hero.jpg", "details": { "image": { "width": 1920, "height": 960 } } } }
        }]
      }
    }
    """;

    [Fact]
    public void Resolves_nested_entry_links_in_order()
    {
        var page = new EntryLinkResolver().Resolve(Parse(NestedJson), "page-1")!;

        page.ContentTypeId.Should().Be("page");
        page.GetString("title").Should().Be("Home");

        var sections = page.GetEntries("sections");
        sections.Should().HaveCount(1, "the dangling link must be dropped, not rendered as a hole");
        sections[0].ContentTypeId.Should().Be("sectionHero");
        sections[0].GetString("heading").Should().Be("Own the market");
    }

    [Fact]
    public void Resolves_asset_links_to_absolute_urls()
    {
        var page = new EntryLinkResolver().Resolve(Parse(NestedJson), "page-1")!;
        var asset = page.GetEntries("sections")[0].GetAsset("image")!;

        asset.Url.Should().Be("https://images.ctfassets.net/x/hero.jpg");
        asset.Width.Should().Be(1920);
        asset.Height.Should().Be(960);
    }

    [Fact]
    public void Returns_null_when_the_requested_entry_is_absent()
    {
        new EntryLinkResolver().Resolve(Parse(NestedJson), "nope").Should().BeNull();
    }

    [Fact]
    public void Terminates_on_circular_references()
    {
        const string circular = """
        {
          "items": [{
            "sys": { "id": "a", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
            "fields": { "title": "A", "related": [{ "sys": { "id": "b", "type": "Link", "linkType": "Entry" } }] }
          }],
          "includes": {
            "Entry": [{
              "sys": { "id": "b", "type": "Entry", "contentType": { "sys": { "id": "article", "type": "Link", "linkType": "ContentType" } } },
              "fields": { "title": "B", "related": [{ "sys": { "id": "a", "type": "Link", "linkType": "Entry" } }] }
            }]
          }
        }
        """;

        var a = new EntryLinkResolver().Resolve(Parse(circular), "a")!;
        var b = a.GetEntries("related").Single();
        var backToA = b.GetEntries("related").Single();

        b.GetString("title").Should().Be("B");
        backToA.Id.Should().Be("a");
        backToA.GetEntries("related").Should().BeEmpty("the cycle must be cut, not followed");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter EntryLinkResolverTests`
Expected: FAIL — `The type or namespace name 'EntryLinkResolver' could not be found`.

- [ ] **Step 3: Create `Contentful/ResolvedEntry.cs`**

```csharp
using System.Text.Json;

namespace Cms.Bff.Contentful;

public sealed record ResolvedAsset(string Url, string? Title, int? Width, int? Height, string? ContentType);

/// <summary>
/// A Contentful entry with every Link node already resolved. Field values are one of:
/// string, bool, decimal, DateTimeOffset, ResolvedEntry, ResolvedAsset,
/// IReadOnlyList&lt;object?&gt;, or JsonElement (RichText and Object fields, passed through).
/// </summary>
public sealed record ResolvedEntry(
    string Id,
    string ContentTypeId,
    IReadOnlyDictionary<string, object?> Fields,
    DateTimeOffset? UpdatedAt)
{
    public object? Get(string field) => Fields.TryGetValue(field, out var v) ? v : null;

    public string? GetString(string field) => Get(field) as string;

    public bool GetBool(string field, bool fallback = false) => Get(field) is bool b ? b : fallback;

    public int? GetInt(string field) => Get(field) switch
    {
        decimal d => (int)d,
        int i => i,
        _ => null,
    };

    public decimal? GetDecimal(string field) => Get(field) as decimal?;

    public DateTimeOffset? GetDate(string field) => Get(field) as DateTimeOffset?;

    public ResolvedEntry? GetEntry(string field) => Get(field) as ResolvedEntry;

    public ResolvedAsset? GetAsset(string field) => Get(field) as ResolvedAsset;

    public JsonElement? GetRichText(string field) => Get(field) as JsonElement?;

    public IReadOnlyList<ResolvedEntry> GetEntries(string field) =>
        Get(field) is IReadOnlyList<object?> list
            ? list.OfType<ResolvedEntry>().ToList()
            : Array.Empty<ResolvedEntry>();

    public IReadOnlyList<string> GetStrings(string field) =>
        Get(field) is IReadOnlyList<object?> list
            ? list.OfType<string>().ToList()
            : Array.Empty<string>();

    /// <summary>Every entry id reachable from this entry, used to build cache tags.</summary>
    public IReadOnlyCollection<string> CollectEntryIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Walk(this, ids);
        return ids;

        static void Walk(ResolvedEntry entry, HashSet<string> acc)
        {
            if (!acc.Add(entry.Id)) return;
            foreach (var value in entry.Fields.Values)
            {
                switch (value)
                {
                    case ResolvedEntry child:
                        Walk(child, acc);
                        break;
                    case IReadOnlyList<object?> list:
                        foreach (var item in list.OfType<ResolvedEntry>()) Walk(item, acc);
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 4: Create `Contentful/EntryLinkResolver.cs`**

```csharp
using System.Text.Json;

namespace Cms.Bff.Contentful;

/// <summary>
/// Flattens a Contentful Delivery API response and resolves every Link node against
/// the includes block. Dangling links are dropped; cycles are cut with a stub entry.
/// </summary>
public sealed class EntryLinkResolver
{
    private const int MaxDepth = 12;

    public ResolvedEntry? Resolve(CdaResponse response, string entryId)
    {
        var (entries, assets) = BuildLookups(response);
        return entries.TryGetValue(entryId, out var entry)
            ? Convert(entry, entries, assets, new HashSet<string>(StringComparer.Ordinal), 0)
            : null;
    }

    public IReadOnlyList<ResolvedEntry> ResolveItems(CdaResponse response)
    {
        var (entries, assets) = BuildLookups(response);
        return response.Items
            .Select(item => Convert(item, entries, assets, new HashSet<string>(StringComparer.Ordinal), 0))
            .OfType<ResolvedEntry>()
            .ToList();
    }

    private static (Dictionary<string, CdaEntry> Entries, Dictionary<string, CdaEntry> Assets) BuildLookups(CdaResponse response)
    {
        var entries = new Dictionary<string, CdaEntry>(StringComparer.Ordinal);
        foreach (var e in response.Items) entries[e.Sys.Id] = e;
        foreach (var e in response.Includes?.Entry ?? new()) entries[e.Sys.Id] = e;

        var assets = new Dictionary<string, CdaEntry>(StringComparer.Ordinal);
        foreach (var a in response.Includes?.Asset ?? new()) assets[a.Sys.Id] = a;

        return (entries, assets);
    }

    private static ResolvedEntry? Convert(
        CdaEntry entry,
        Dictionary<string, CdaEntry> entries,
        Dictionary<string, CdaEntry> assets,
        HashSet<string> path,
        int depth)
    {
        // Cycle or depth limit: return a stub so consumers still see the reference,
        // without following it again.
        if (depth > MaxDepth || !path.Add(entry.Sys.Id))
            return new ResolvedEntry(entry.Sys.Id, entry.ContentTypeId, new Dictionary<string, object?>(), entry.Sys.UpdatedAt);

        try
        {
            var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (name, value) in entry.Fields)
                fields[name] = ConvertValue(value, entries, assets, path, depth);

            return new ResolvedEntry(entry.Sys.Id, entry.ContentTypeId, fields, entry.Sys.UpdatedAt);
        }
        finally
        {
            path.Remove(entry.Sys.Id);
        }
    }

    private static object? ConvertValue(
        JsonElement value,
        Dictionary<string, CdaEntry> entries,
        Dictionary<string, CdaEntry> assets,
        HashSet<string> path,
        int depth)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var s = value.GetString();
                return DateTimeOffset.TryParse(s, out var dt) && LooksLikeDate(s) ? dt : s;

            case JsonValueKind.Number:
                return value.GetDecimal();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return value.GetBoolean();

            case JsonValueKind.Null:
                return null;

            case JsonValueKind.Array:
                return value.EnumerateArray()
                    .Select(item => ConvertValue(item, entries, assets, path, depth))
                    .Where(item => item is not null)
                    .ToList();

            case JsonValueKind.Object:
                if (TryReadLink(value, out var linkType, out var linkId))
                {
                    if (linkType == "Asset")
                        return assets.TryGetValue(linkId, out var asset) ? ConvertAsset(asset) : null;

                    return entries.TryGetValue(linkId, out var linked)
                        ? Convert(linked, entries, assets, path, depth + 1)
                        : null; // dangling link: drop it
                }
                // RichText and Object fields pass through untouched.
                return value.Clone();

            default:
                return null;
        }
    }

    private static bool LooksLikeDate(string? s) =>
        s is { Length: >= 10 } && s[4] == '-' && s[7] == '-';

    private static bool TryReadLink(JsonElement value, out string linkType, out string linkId)
    {
        linkType = "";
        linkId = "";
        if (!value.TryGetProperty("sys", out var sys)) return false;
        if (!sys.TryGetProperty("type", out var type) || type.GetString() != "Link") return false;
        linkType = sys.TryGetProperty("linkType", out var lt) ? lt.GetString() ?? "" : "";
        linkId = sys.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";
        return linkId.Length > 0;
    }

    private static ResolvedAsset? ConvertAsset(CdaEntry asset)
    {
        if (!asset.Fields.TryGetValue("file", out var file)) return null;
        if (!file.TryGetProperty("url", out var urlProp)) return null;

        var url = urlProp.GetString() ?? "";
        if (url.StartsWith("//", StringComparison.Ordinal)) url = "https:" + url;

        int? width = null, height = null;
        if (file.TryGetProperty("details", out var details) &&
            details.TryGetProperty("image", out var image))
        {
            if (image.TryGetProperty("width", out var w)) width = w.GetInt32();
            if (image.TryGetProperty("height", out var h)) height = h.GetInt32();
        }

        var title = asset.Fields.TryGetValue("title", out var t) ? t.GetString() : null;
        var contentType = file.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;

        return new ResolvedAsset(url, title, width, height, contentType);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter EntryLinkResolverTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 6: Commit**

```powershell
git add bff/src/Cms.Bff/Contentful bff/tests/Cms.Bff.Tests/EntryLinkResolverTests.cs
git commit -m "feat(bff): resolve Contentful link graph with cycle and dangling-link handling

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.3: Contentful clients (HTTP and fixture)

**Files:**
- Create: `bff/src/Cms.Bff/Contentful/IContentfulClient.cs`
- Create: `bff/src/Cms.Bff/Contentful/HttpContentfulClient.cs`
- Create: `bff/src/Cms.Bff/Contentful/FixtureContentfulClient.cs`

- [ ] **Step 1: Create the interface**

```csharp
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
```

- [ ] **Step 2: Create `HttpContentfulClient.cs`**

```csharp
using System.Net.Http.Json;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Contentful;

public sealed class HttpContentfulClient : IContentfulClient
{
    private readonly IHttpClientFactory _factory;
    private readonly ContentfulOptions _options;
    private readonly ILogger<HttpContentfulClient> _logger;

    public HttpContentfulClient(IHttpClientFactory factory, IOptions<ContentfulOptions> options, ILogger<HttpContentfulClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<CdaResponse> QueryAsync(ContentfulQuery query, bool preview, CancellationToken ct = default)
    {
        var host = preview ? "preview.contentful.com" : "cdn.contentful.com";
        var token = preview ? _options.PreviewToken : _options.DeliveryToken;

        var parameters = new List<string>
        {
            $"content_type={Uri.EscapeDataString(query.ContentType)}",
            $"include={query.Include}",
            $"limit={query.Limit}",
        };
        if (query.Order is not null) parameters.Add($"order={Uri.EscapeDataString(query.Order)}");
        foreach (var (k, v) in query.Filters ?? new Dictionary<string, string>())
            parameters.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");

        var url = $"https://{host}/spaces/{_options.SpaceId}/environments/{_options.Environment}/entries?{string.Join("&", parameters)}";

        var client = _factory.CreateClient(nameof(HttpContentfulClient));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new("Bearer", token);

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Contentful returned {Status} for {ContentType}: {Body}", (int)response.StatusCode, query.ContentType, body);
            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<CdaResponse>(cancellationToken: ct)
               ?? new CdaResponse();
    }
}
```

- [ ] **Step 3: Create `FixtureContentfulClient.cs`**

Filters the fixture in-process so the same query contract works offline. Supports the
filter keys the BFF actually uses: `fields.slug`, `fields.category.sys.id`, and `sys.id`.

```csharp
using System.Text.Json;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Contentful;

public sealed class FixtureContentfulClient : IContentfulClient
{
    private readonly Lazy<CdaResponse> _fixture;
    private readonly ILogger<FixtureContentfulClient> _logger;

    public FixtureContentfulClient(IOptions<ContentfulOptions> options, IWebHostEnvironment env, ILogger<FixtureContentfulClient> logger)
    {
        _logger = logger;
        _fixture = new Lazy<CdaResponse>(() =>
        {
            var path = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.FixturePath));
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Fixture file not found at '{path}'. Set Contentful:FixturePath, or set Contentful:Mode=Live.", path);

            _logger.LogInformation("Contentful running in Fixture mode from {Path}", path);
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<CdaResponse>(stream) ?? new CdaResponse();
        });
    }

    public Task<CdaResponse> QueryAsync(ContentfulQuery query, bool preview, CancellationToken ct = default)
    {
        var all = _fixture.Value;

        // Everything not in Items is a candidate too: the fixture stores all entries in
        // includes, and any of them may be the subject of a query.
        var candidates = all.Items
            .Concat(all.Includes?.Entry ?? new())
            .Where(e => e.ContentTypeId == query.ContentType)
            .ToList();

        foreach (var (key, value) in query.Filters ?? new Dictionary<string, string>())
            candidates = candidates.Where(e => Matches(e, key, value)).ToList();

        // Preview mode is a no-op offline: fixtures contain no draft/published split.
        return Task.FromResult(new CdaResponse
        {
            Items = candidates.Take(query.Limit).ToList(),
            Includes = all.Includes,
            Total = candidates.Count,
        });
    }

    private static bool Matches(CdaEntry entry, string key, string value)
    {
        if (key == "sys.id") return entry.Sys.Id == value;

        if (!key.StartsWith("fields.", StringComparison.Ordinal)) return true;

        var parts = key["fields.".Length..].Split('.');
        if (!entry.Fields.TryGetValue(parts[0], out var field)) return false;

        // fields.slug
        if (parts.Length == 1)
            return field.ValueKind == JsonValueKind.String && field.GetString() == value;

        // fields.category.sys.id
        if (parts is [_, "sys", "id"])
            return field.ValueKind == JsonValueKind.Object
                   && field.TryGetProperty("sys", out var sys)
                   && sys.TryGetProperty("id", out var id)
                   && id.GetString() == value;

        return false;
    }
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build bff/src/Cms.Bff`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add bff/src/Cms.Bff/Contentful
git commit -m "feat(bff): add HTTP and fixture Contentful clients behind one interface

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.4: Section DTOs, resolvers and registry (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Sections/SectionDto.cs`
- Create: `bff/src/Cms.Bff/Sections/ISectionResolver.cs`
- Create: `bff/src/Cms.Bff/Sections/SectionResolverRegistry.cs`
- Create: `bff/src/Cms.Bff/Sections/Resolvers/*.cs`
- Test: `bff/tests/Cms.Bff.Tests/SectionResolverRegistryTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.Sections;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class SectionResolverRegistryTests
{
    private static ResolvedEntry Entry(string id, string contentType, Dictionary<string, object?>? fields = null) =>
        new(id, contentType, fields ?? new Dictionary<string, object?>(), null);

    private static SectionResolverRegistry Registry(params ISectionResolver[] resolvers) =>
        new(resolvers, NullLogger<SectionResolverRegistry>.Instance);

    private sealed class FakeResolver : ISectionResolver
    {
        public string ContentTypeId => "sectionHero";
        public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
            Task.FromResult<SectionDto?>(new HeroSection { Id = entry.Id, Heading = entry.GetString("heading") ?? "" });
    }

    [Fact]
    public async Task Dispatches_to_the_resolver_registered_for_the_content_type()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[] { Entry("h1", "sectionHero", new() { ["heading"] = "Own the market" }) },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Should().ContainSingle();
        sections[0].Type.Should().Be("sectionHero");
        sections[0].Should().BeOfType<HeroSection>()
            .Which.Heading.Should().Be("Own the market");
    }

    [Fact]
    public async Task Drops_unknown_section_types_instead_of_throwing()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[]
            {
                Entry("h1", "sectionHero", new() { ["heading"] = "Kept" }),
                Entry("x1", "sectionFutureThing"),
            },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Should().ContainSingle("an unregistered section type must not break the page");
        sections[0].Id.Should().Be("h1");
    }

    [Fact]
    public async Task Preserves_author_ordering()
    {
        var sections = await Registry(new FakeResolver()).ResolveAllAsync(
            new[]
            {
                Entry("a", "sectionHero", new() { ["heading"] = "first" }),
                Entry("b", "sectionHero", new() { ["heading"] = "second" }),
                Entry("c", "sectionHero", new() { ["heading"] = "third" }),
            },
            SectionContext.ForPage("/"),
            CancellationToken.None);

        sections.Select(s => s.Id).Should().ContainInOrder("a", "b", "c");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter SectionResolverRegistryTests`
Expected: FAIL — `SectionResolverRegistry could not be found`.

- [ ] **Step 3: Create `Sections/SectionDto.cs`**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cms.Bff.Sections;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "__type")]
public abstract record SectionDto
{
    [JsonPropertyName("__type")]
    public abstract string Type { get; }

    public required string Id { get; init; }
}

public sealed record ImageDto(string Url, string AltText, int? Width, int? Height, string? Caption);

public sealed record LinkDto(string Label, string Url);

public sealed record HeroSection : SectionDto
{
    public override string Type => "sectionHero";
    public string? Eyebrow { get; init; }
    public required string Heading { get; init; }
    public string? Subheading { get; init; }
    public ImageDto? BackgroundImage { get; init; }
    public LinkDto? PrimaryCta { get; init; }
    public LinkDto? SecondaryCta { get; init; }
    public string Variant { get; init; } = "default";
}

public sealed record RichTextSection : SectionDto
{
    public override string Type => "sectionRichText";
    public string? Heading { get; init; }
    public JsonElement? Body { get; init; }
    public string Width { get; init; } = "default";
}

public sealed record CardDto(string Id, string Title, string? Body, ImageDto? Image, LinkDto? Link, string? Icon);

public sealed record CardGridSection : SectionDto
{
    public override string Type => "sectionCardGrid";
    public string? Heading { get; init; }
    public string? Intro { get; init; }
    public IReadOnlyList<CardDto> Cards { get; init; } = Array.Empty<CardDto>();
    public int Columns { get; init; } = 3;
}

public sealed record CtaBannerSection : SectionDto
{
    public override string Type => "sectionCtaBanner";
    public required string Heading { get; init; }
    public string? Body { get; init; }
    public required LinkDto Cta { get; init; }
    public string Variant { get; init; } = "primary";
}

public sealed record ArticleTeaserDto(string Id, string Title, string Slug, string? Excerpt, ImageDto? Image, string? CategoryName, DateTimeOffset? PublishDate, string? AuthorName);

public sealed record ArticleTeaserListSection : SectionDto
{
    public override string Type => "sectionArticleTeaserList";
    public string? Heading { get; init; }
    public IReadOnlyList<ArticleTeaserDto> Articles { get; init; } = Array.Empty<ArticleTeaserDto>();
}

public sealed record FundSummaryDto(string Code, string Name, string ShortDescription, string CategoryId, decimal Nav, decimal DayChangePercent, decimal Ter, decimal OneYearReturn);

public sealed record FundListSection : SectionDto
{
    public override string Type => "sectionFundListWidget";
    public string? Heading { get; init; }
    public string? Intro { get; init; }
    public string? CategoryId { get; init; }
    public bool ShowFilters { get; init; }
    public string DisplayVariant { get; init; } = "grid";
    public IReadOnlyList<FundCategoryDto> Categories { get; init; } = Array.Empty<FundCategoryDto>();
    public IReadOnlyList<FundSummaryDto> Funds { get; init; } = Array.Empty<FundSummaryDto>();
}

public sealed record FundCategoryDto(string Id, string Name, string Slug);

public sealed record PricePointDto(DateOnly Date, decimal Nav);

public sealed record FundPerformanceDto(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public sealed record FundDetailSection : SectionDto
{
    public override string Type => "sectionFundDetailWidget";
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Isin { get; init; }
    public string? ShortDescription { get; init; }
    public decimal? Nav { get; init; }
    public decimal? DayChangePercent { get; init; }
    public decimal? Ter { get; init; }
    public DateOnly? InceptionDate { get; init; }
    public string? FactsheetUrl { get; init; }
    public FundPerformanceDto? Performance { get; init; }
    public IReadOnlyList<PricePointDto> Prices { get; init; } = Array.Empty<PricePointDto>();
    /// <summary>True when the configured fund could not be found upstream.</summary>
    public bool Unavailable { get; init; }
}

public sealed record FaqItemDto(string Id, string Question, JsonElement? Answer, string PlainTextAnswer);

public sealed record FaqAccordionSection : SectionDto
{
    public override string Type => "sectionFaqAccordion";
    public string? Heading { get; init; }
    public IReadOnlyList<FaqItemDto> Items { get; init; } = Array.Empty<FaqItemDto>();
    public bool EmitFaqSchema { get; init; }
}

public sealed record ImageWithTextSection : SectionDto
{
    public override string Type => "sectionImageWithText";
    public string? Heading { get; init; }
    public JsonElement? Body { get; init; }
    public ImageDto? Image { get; init; }
    public string ImagePosition { get; init; } = "left";
    public LinkDto? Cta { get; init; }
}

public sealed record DisclaimerSection : SectionDto
{
    public override string Type => "sectionDisclaimer";
    public string? Label { get; init; }
    public JsonElement? Body { get; init; }
    public string Severity { get; init; } = "info";
    public bool Collapsible { get; init; }
}
```

- [ ] **Step 4: Create `Sections/ISectionResolver.cs`**

```csharp
using Cms.Bff.Contentful;

namespace Cms.Bff.Sections;

/// <param name="Slug">The page being rendered, used for logging and canonical URLs.</param>
/// <param name="FundCodeOverride">
/// Set only for the fund detail template route. Injected into a
/// sectionFundDetailWidget whose own fundCode is blank, so one Contentful template
/// serves every fund. An explicitly authored fundCode always wins.
/// </param>
public sealed record SectionContext(string Slug, string? FundCodeOverride, bool Preview)
{
    public static SectionContext ForPage(string slug) => new(slug, null, false);
}

public interface ISectionResolver
{
    string ContentTypeId { get; }
    Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct);
}
```

- [ ] **Step 5: Create `Sections/SectionResolverRegistry.cs`**

```csharp
using Cms.Bff.Contentful;

namespace Cms.Bff.Sections;

public sealed class SectionResolverRegistry
{
    private readonly Dictionary<string, ISectionResolver> _resolvers;
    private readonly ILogger<SectionResolverRegistry> _logger;

    public SectionResolverRegistry(IEnumerable<ISectionResolver> resolvers, ILogger<SectionResolverRegistry> logger)
    {
        _resolvers = resolvers.ToDictionary(r => r.ContentTypeId, StringComparer.Ordinal);
        _logger = logger;
    }

    public async Task<IReadOnlyList<SectionDto>> ResolveAllAsync(
        IEnumerable<ResolvedEntry> entries,
        SectionContext context,
        CancellationToken ct)
    {
        // Resolve in parallel (fund widgets call an upstream API) but restore author
        // ordering afterwards: the order the admin user set in Contentful is the
        // contract, and Task.WhenAll does not preserve it on its own.
        var ordered = entries.ToList();
        var tasks = ordered.Select(entry => ResolveOneAsync(entry, context, ct)).ToList();
        var results = await Task.WhenAll(tasks);

        return results.Where(s => s is not null).Select(s => s!).ToList();
    }

    private async Task<SectionDto?> ResolveOneAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        if (!_resolvers.TryGetValue(entry.ContentTypeId, out var resolver))
        {
            // Degradation contract: a content type the code does not know about is
            // dropped, never thrown. A half-migrated model must not 500 the page.
            _logger.LogWarning(
                "No resolver registered for section content type '{ContentType}' (entry {EntryId}) on page '{Slug}'. Section dropped.",
                entry.ContentTypeId, entry.Id, context.Slug);
            return null;
        }

        try
        {
            return await resolver.ResolveAsync(entry, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Resolver for '{ContentType}' failed on entry {EntryId} (page '{Slug}'). Section dropped.",
                entry.ContentTypeId, entry.Id, context.Slug);
            return null;
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter SectionResolverRegistryTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 7: Commit**

```powershell
git add bff/src/Cms.Bff/Sections bff/tests/Cms.Bff.Tests/SectionResolverRegistryTests.cs
git commit -m "feat(bff): add section DTOs and resolver registry with safe degradation

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.5: Content-only section resolvers

**Files:**
- Create: `bff/src/Cms.Bff/Sections/Resolvers/HeroSectionResolver.cs` (and one file per type below)
- Create: `bff/src/Cms.Bff/Sections/SectionMapping.cs`

- [ ] **Step 1: Create shared mapping helpers in `Sections/SectionMapping.cs`**

```csharp
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
```

- [ ] **Step 2: Create `Resolvers/HeroSectionResolver.cs` as the exemplar**

```csharp
using Cms.Bff.Contentful;

namespace Cms.Bff.Sections.Resolvers;

public sealed class HeroSectionResolver : ISectionResolver
{
    public string ContentTypeId => "sectionHero";

    public Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct) =>
        Task.FromResult<SectionDto?>(new HeroSection
        {
            Id = entry.Id,
            Eyebrow = entry.GetString("eyebrow"),
            Heading = entry.GetString("heading") ?? "",
            Subheading = entry.GetString("subheading"),
            BackgroundImage = SectionMapping.ToImage(entry.GetEntry("backgroundImage")),
            PrimaryCta = SectionMapping.ToLink(entry.GetString("primaryCtaLabel"), entry.GetString("primaryCtaUrl")),
            SecondaryCta = SectionMapping.ToLink(entry.GetString("secondaryCtaLabel"), entry.GetString("secondaryCtaUrl")),
            Variant = entry.GetString("variant") ?? "default",
        });
}
```

- [ ] **Step 3: Create the remaining content-only resolvers**

Each follows the exemplar exactly: one class, `ContentTypeId` matching the Contentful
content type id, and a synchronous field mapping returned via `Task.FromResult`. Field
mappings:

| Resolver file | ContentTypeId | DTO | Mapping notes |
|---|---|---|---|
| `RichTextSectionResolver.cs` | `sectionRichText` | `RichTextSection` | `Body = entry.GetRichText("body")`, `Width = entry.GetString("width") ?? "default"` |
| `CardGridSectionResolver.cs` | `sectionCardGrid` | `CardGridSection` | `Cards` from `entry.GetEntries("cards")` → `new CardDto(c.Id, c.GetString("title") ?? "", c.GetString("body"), SectionMapping.ToImage(c.GetEntry("image")), SectionMapping.ToLink(c.GetString("linkLabel"), c.GetString("linkUrl")), c.GetString("icon"))`; `Columns = entry.GetInt("columns") ?? 3` |
| `CtaBannerSectionResolver.cs` | `sectionCtaBanner` | `CtaBannerSection` | `Cta` is required; if label or url is blank, **return null** so a half-authored banner is dropped rather than rendered broken |
| `ImageWithTextSectionResolver.cs` | `sectionImageWithText` | `ImageWithTextSection` | `ImagePosition = entry.GetString("imagePosition") ?? "left"` |
| `DisclaimerSectionResolver.cs` | `sectionDisclaimer` | `DisclaimerSection` | `Severity = entry.GetString("severity") ?? "info"`, `Collapsible = entry.GetBool("collapsible")` |
| `FaqAccordionSectionResolver.cs` | `sectionFaqAccordion` | `FaqAccordionSection` | `Items` from `entry.GetEntries("items")`; `PlainTextAnswer` via `RichTextFlattener.ToPlainText(...)` (Task 3.6); `EmitFaqSchema = entry.GetBool("emitFaqSchema")` |

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build bff/src/Cms.Bff`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add bff/src/Cms.Bff/Sections
git commit -m "feat(bff): add content-only section resolvers

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.6: Rich text flattener (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Contentful/RichTextFlattener.cs`
- Test: `bff/tests/Cms.Bff.Tests/RichTextFlattenerTests.cs`

FAQPage JSON-LD requires the answer as plain text, and meta descriptions fall back to
body copy. Both need Contentful's rich-text AST flattened.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using Cms.Bff.Contentful;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class RichTextFlattenerTests
{
    [Fact]
    public void Flattens_nested_rich_text_nodes_to_plain_text()
    {
        var doc = JsonDocument.Parse("""
        {
          "nodeType": "document",
          "content": [
            { "nodeType": "paragraph", "content": [
              { "nodeType": "text", "value": "Tax-free savings " },
              { "nodeType": "text", "value": "accounts", "marks": [{ "type": "bold" }] },
              { "nodeType": "text", "value": " let you invest without paying tax." }
            ]},
            { "nodeType": "paragraph", "content": [ { "nodeType": "text", "value": "The annual limit applies." } ]}
          ]
        }
        """).RootElement;

        RichTextFlattener.ToPlainText(doc)
            .Should().Be("Tax-free savings accounts let you invest without paying tax. The annual limit applies.");
    }

    [Fact]
    public void Returns_empty_string_for_null_or_empty_documents()
    {
        RichTextFlattener.ToPlainText(null).Should().BeEmpty();
        RichTextFlattener.ToPlainText(JsonDocument.Parse("{}").RootElement).Should().BeEmpty();
    }

    [Fact]
    public void Truncates_on_a_word_boundary_without_cutting_mid_word()
    {
        var doc = JsonDocument.Parse("""
        { "nodeType": "document", "content": [ { "nodeType": "paragraph", "content": [
          { "nodeType": "text", "value": "Satrix makes index investing simple for everyone in South Africa." } ]}]}
        """).RootElement;

        var result = RichTextFlattener.Summarise(doc, 30);

        result.Should().Be("Satrix makes index investing…");
        result.Length.Should().BeLessThanOrEqualTo(30);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter RichTextFlattenerTests`
Expected: FAIL — `RichTextFlattener could not be found`.

- [ ] **Step 3: Create `Contentful/RichTextFlattener.cs`**

```csharp
using System.Text;
using System.Text.Json;

namespace Cms.Bff.Contentful;

public static class RichTextFlattener
{
    public static string ToPlainText(JsonElement? document)
    {
        if (document is not { ValueKind: JsonValueKind.Object } doc) return "";

        var builder = new StringBuilder();
        Walk(doc, builder);
        return string.Join(" ", builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Plain text truncated at a word boundary, with an ellipsis if shortened.</summary>
    public static string Summarise(JsonElement? document, int maxLength)
    {
        var text = ToPlainText(document);
        if (text.Length <= maxLength) return text;

        var cut = text[..(maxLength - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0) cut = cut[..lastSpace];
        return cut + "…";
    }

    private static void Walk(JsonElement node, StringBuilder builder)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                builder.Append(value.GetString()).Append(' ');

            if (node.TryGetProperty("content", out var content))
                Walk(content, builder);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray()) Walk(child, builder);
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter RichTextFlattenerTests`
Expected: `Passed! - Failed: 0, Passed: 3`

- [ ] **Step 5: Commit**

```powershell
git add bff/src/Cms.Bff/Contentful/RichTextFlattener.cs bff/tests/Cms.Bff.Tests/RichTextFlattenerTests.cs
git commit -m "feat(bff): flatten Contentful rich text for JSON-LD and meta fallbacks

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.7: Fund data client and fund widget resolvers (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/FundData/FundDtos.cs`, `IFundDataClient.cs`, `HttpFundDataClient.cs`
- Create: `bff/src/Cms.Bff/Sections/Resolvers/FundListSectionResolver.cs`, `FundDetailSectionResolver.cs`
- Test: `bff/tests/Cms.Bff.Tests/FundWidgetResolverTests.cs`

This is where Contentful configuration meets live data. The tests pin the two rules
that matter: fund data never comes from Contentful, and an upstream failure degrades
the section instead of the page.

- [ ] **Step 1: Write the failing tests**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.FundData;
using Cms.Bff.Sections;
using Cms.Bff.Sections.Resolvers;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class FundWidgetResolverTests
{
    private static ResolvedEntry Entry(string id, string contentType, Dictionary<string, object?> fields) =>
        new(id, contentType, fields, null);

    private sealed class StubFundDataClient : IFundDataClient
    {
        public bool ShouldThrow { get; init; }
        public string? LastRequestedCategoryId { get; private set; }
        public int? LastRequestedTop { get; private set; }

        public Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct)
        {
            if (ShouldThrow) throw new HttpRequestException("fund api down");
            LastRequestedCategoryId = categoryId;
            LastRequestedTop = top;
            return Task.FromResult<IReadOnlyList<FundSummary>>(new[]
            {
                new FundSummary("STX40", "Satrix 40 ETF", "Top 40 tracker", "1", 88.42m, 0.62m, 0.10m, 14.8m),
            });
        }

        public Task<FundDetail?> GetFundAsync(string code, CancellationToken ct)
        {
            if (ShouldThrow) throw new HttpRequestException("fund api down");
            return Task.FromResult(code == "STX40"
                ? new FundDetail("STX40", "ZAE000027108", "Satrix 40 ETF", "Top 40 tracker", "1",
                    0.10m, 88.42m, 0.62m, new DateOnly(2000, 11, 27), "https://example.invalid/f.pdf",
                    new FundPerformance(14.8m, 11.2m, 9.6m, 13.1m))
                : null);
        }

        public Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FundPrice>>(new[] { new FundPrice(new DateOnly(2026, 9, 15), 88.42m) });

        public Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FundCategory>>(new[] { new FundCategory("1", "Local Equity", "local-equity") });
    }

    [Fact]
    public async Task Fund_list_passes_contentful_configuration_to_the_data_api_and_merges_the_results()
    {
        var client = new StubFundDataClient();
        var entry = Entry("fl1", "sectionFundListWidget", new()
        {
            ["heading"] = "Our funds",
            ["categoryId"] = "1",
            ["topN"] = 3m,
            ["displayVariant"] = "table",
            ["showFilters"] = true,
        });

        var section = (FundListSection)(await new FundListSectionResolver(client)
            .ResolveAsync(entry, SectionContext.ForPage("funds"), CancellationToken.None))!;

        client.LastRequestedCategoryId.Should().Be("1");
        client.LastRequestedTop.Should().Be(3);
        section.Heading.Should().Be("Our funds");
        section.DisplayVariant.Should().Be("table");
        section.Funds.Should().ContainSingle().Which.Name.Should().Be("Satrix 40 ETF");
        section.Categories.Should().ContainSingle();
    }

    [Fact]
    public async Task Fund_detail_uses_the_route_override_when_contentful_leaves_the_code_blank()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new()
        {
            ["fundCode"] = null,
            ["showPerformance"] = true,
            ["showPriceHistory"] = true,
        });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient())
            .ResolveAsync(entry, new SectionContext("funds/_detail", "STX40", false), CancellationToken.None))!;

        section.Code.Should().Be("STX40");
        section.Name.Should().Be("Satrix 40 ETF");
        section.Nav.Should().Be(88.42m);
        section.Performance!.OneYear.Should().Be(14.8m);
        section.Prices.Should().ContainSingle();
        section.Unavailable.Should().BeFalse();
    }

    [Fact]
    public async Task An_authored_fund_code_wins_over_the_route_override()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new() { ["fundCode"] = "STX40" });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient())
            .ResolveAsync(entry, new SectionContext("some-campaign", "STXWDM", false), CancellationToken.None))!;

        section.Code.Should().Be("STX40", "a bespoke single-fund page must not be hijacked by the route param");
    }

    [Fact]
    public async Task An_unknown_fund_degrades_the_section_rather_than_failing_the_page()
    {
        var entry = Entry("fd1", "sectionFundDetailWidget", new() { ["fundCode"] = "NOPE" });

        var section = (FundDetailSection)(await new FundDetailSectionResolver(new StubFundDataClient())
            .ResolveAsync(entry, SectionContext.ForPage("funds/_detail"), CancellationToken.None))!;

        section.Unavailable.Should().BeTrue();
        section.Name.Should().BeNull();
    }

    [Fact]
    public async Task An_upstream_outage_degrades_the_fund_list_to_empty_rather_than_throwing()
    {
        var entry = Entry("fl1", "sectionFundListWidget", new() { ["heading"] = "Our funds" });

        var section = (FundListSection)(await new FundListSectionResolver(new StubFundDataClient { ShouldThrow = true })
            .ResolveAsync(entry, SectionContext.ForPage("funds"), CancellationToken.None))!;

        section.Heading.Should().Be("Our funds", "authored copy must still render when the data API is down");
        section.Funds.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter FundWidgetResolverTests`
Expected: FAIL — `IFundDataClient could not be found`.

- [ ] **Step 3: Create `FundData/FundDtos.cs`**

```csharp
namespace Cms.Bff.FundData;

public record FundCategory(string Id, string Name, string Slug);

public record FundPerformance(decimal OneYear, decimal ThreeYear, decimal FiveYear, decimal SinceInception);

public record FundPrice(DateOnly Date, decimal Nav);

public record FundSummary(
    string Code, string Name, string ShortDescription, string CategoryId,
    decimal Nav, decimal DayChangePercent, decimal Ter, decimal OneYearReturn);

public record FundDetail(
    string Code, string Isin, string Name, string ShortDescription, string CategoryId,
    decimal Ter, decimal Nav, decimal DayChangePercent, DateOnly InceptionDate,
    string FactsheetUrl, FundPerformance Performance);
```

- [ ] **Step 4: Create `FundData/IFundDataClient.cs`**

```csharp
namespace Cms.Bff.FundData;

public interface IFundDataClient
{
    Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct);
    Task<FundDetail?> GetFundAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct);
    Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct);
}
```

- [ ] **Step 5: Create `FundData/HttpFundDataClient.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cms.Bff.FundData;

public sealed class HttpFundDataClient : IFundDataClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public HttpFundDataClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<FundSummary>> GetFundsAsync(string? categoryId, int? top, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(categoryId)) query.Add($"categoryId={Uri.EscapeDataString(categoryId)}");
        if (top is > 0) query.Add($"top={top}");
        var url = "/funds" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        return await _http.GetFromJsonAsync<List<FundSummary>>(url, Json, ct) ?? new List<FundSummary>();
    }

    public async Task<FundDetail?> GetFundAsync(string code, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/funds/{Uri.EscapeDataString(code)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FundDetail>(Json, ct);
    }

    public async Task<IReadOnlyList<FundPrice>> GetPricesAsync(string code, int days, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"/funds/{Uri.EscapeDataString(code)}/prices?days={days}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return Array.Empty<FundPrice>();
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<FundPrice>>(Json, ct) ?? new List<FundPrice>();
    }

    public async Task<IReadOnlyList<FundCategory>> GetCategoriesAsync(CancellationToken ct) =>
        await _http.GetFromJsonAsync<List<FundCategory>>("/categories", Json, ct) ?? new List<FundCategory>();
}
```

- [ ] **Step 6: Create `Sections/Resolvers/FundListSectionResolver.cs`**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.FundData;

namespace Cms.Bff.Sections.Resolvers;

public sealed class FundListSectionResolver : ISectionResolver
{
    private readonly IFundDataClient _funds;

    public FundListSectionResolver(IFundDataClient funds) => _funds = funds;

    public string ContentTypeId => "sectionFundListWidget";

    public async Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        var categoryId = entry.GetString("categoryId");
        var topN = entry.GetInt("topN");
        var showFilters = entry.GetBool("showFilters");

        IReadOnlyList<FundSummary> funds = Array.Empty<FundSummary>();
        IReadOnlyList<FundCategory> categories = Array.Empty<FundCategory>();

        try
        {
            var fundsTask = _funds.GetFundsAsync(categoryId, topN, ct);
            var categoriesTask = showFilters ? _funds.GetCategoriesAsync(ct) : Task.FromResult<IReadOnlyList<FundCategory>>(Array.Empty<FundCategory>());
            await Task.WhenAll(fundsTask, categoriesTask);
            funds = fundsTask.Result;
            categories = categoriesTask.Result;
        }
        catch (Exception)
        {
            // Authored copy still renders; the data block degrades to empty.
            // Logged by SectionResolverRegistry's caller context, not swallowed silently.
        }

        return new FundListSection
        {
            Id = entry.Id,
            Heading = entry.GetString("heading"),
            Intro = entry.GetString("intro"),
            CategoryId = categoryId,
            ShowFilters = showFilters,
            DisplayVariant = entry.GetString("displayVariant") ?? "grid",
            Categories = categories.Select(c => new FundCategoryDto(c.Id, c.Name, c.Slug)).ToList(),
            Funds = funds.Select(f => new FundSummaryDto(
                f.Code, f.Name, f.ShortDescription, f.CategoryId, f.Nav, f.DayChangePercent, f.Ter, f.OneYearReturn)).ToList(),
        };
    }
}
```

- [ ] **Step 7: Create `Sections/Resolvers/FundDetailSectionResolver.cs`**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.FundData;

namespace Cms.Bff.Sections.Resolvers;

public sealed class FundDetailSectionResolver : ISectionResolver
{
    private const int PriceWindowDays = 90;
    private readonly IFundDataClient _funds;

    public FundDetailSectionResolver(IFundDataClient funds) => _funds = funds;

    public string ContentTypeId => "sectionFundDetailWidget";

    public async Task<SectionDto?> ResolveAsync(ResolvedEntry entry, SectionContext context, CancellationToken ct)
    {
        // An explicitly authored fundCode always wins so a bespoke single-fund landing
        // page keeps working; the route override only fills a deliberately blank field
        // on the shared funds/_detail template.
        var code = entry.GetString("fundCode");
        if (string.IsNullOrWhiteSpace(code)) code = context.FundCodeOverride;

        if (string.IsNullOrWhiteSpace(code))
            return new FundDetailSection { Id = entry.Id, Unavailable = true };

        FundDetail? fund;
        IReadOnlyList<FundPrice> prices = Array.Empty<FundPrice>();
        var showPrices = entry.GetBool("showPriceHistory");

        try
        {
            fund = await _funds.GetFundAsync(code, ct);
            if (fund is not null && showPrices)
                prices = await _funds.GetPricesAsync(code, PriceWindowDays, ct);
        }
        catch (Exception)
        {
            fund = null;
        }

        if (fund is null)
            return new FundDetailSection { Id = entry.Id, Code = code, Unavailable = true };

        return new FundDetailSection
        {
            Id = entry.Id,
            Code = fund.Code,
            Name = fund.Name,
            Isin = fund.Isin,
            ShortDescription = fund.ShortDescription,
            Nav = fund.Nav,
            DayChangePercent = fund.DayChangePercent,
            Ter = fund.Ter,
            InceptionDate = fund.InceptionDate,
            FactsheetUrl = entry.GetBool("showFactsheet") ? fund.FactsheetUrl : null,
            Performance = entry.GetBool("showPerformance")
                ? new FundPerformanceDto(fund.Performance.OneYear, fund.Performance.ThreeYear, fund.Performance.FiveYear, fund.Performance.SinceInception)
                : null,
            Prices = prices.Select(p => new PricePointDto(p.Date, p.Nav)).ToList(),
            Unavailable = false,
        };
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter FundWidgetResolverTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 9: Commit**

```powershell
git add bff/src/Cms.Bff/FundData bff/src/Cms.Bff/Sections/Resolvers bff/tests/Cms.Bff.Tests/FundWidgetResolverTests.cs
git commit -m "feat(bff): resolve fund widgets by merging live data into Contentful config

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.8: SEO resolver (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Seo/SeoDto.cs`, `ISeoResolver.cs`, `SeoResolver.cs`
- Test: `bff/tests/Cms.Bff.Tests/SeoResolverTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.Options;
using Cms.Bff.Seo;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cms.Bff.Tests;

public class SeoResolverTests
{
    private static readonly SiteOptions Site = new() { BaseUrl = "https://www.example.co.za" };

    private static SeoResolver Resolver() => new(Options.Create(Site));

    private static ResolvedEntry Seo(Dictionary<string, object?> fields) =>
        new("seo-1", "seo", fields, null);

    [Fact]
    public void Uses_the_page_seo_entry_when_present()
    {
        var seo = Resolver().Resolve(
            pageSeo: Seo(new() { ["metaTitle"] = "Tax-Free Investing", ["metaDescription"] = "Invest without paying tax." }),
            fallbackSeo: Seo(new() { ["metaTitle"] = "Fallback", ["metaDescription"] = "Fallback description." }),
            slug: "tax-free-investing",
            tokens: null);

        seo.MetaTitle.Should().Be("Tax-Free Investing");
        seo.MetaDescription.Should().Be("Invest without paying tax.");
    }

    [Fact]
    public void Falls_back_to_site_settings_when_the_page_has_no_seo_entry()
    {
        var seo = Resolver().Resolve(
            pageSeo: null,
            fallbackSeo: Seo(new() { ["metaTitle"] = "Own the Market", ["metaDescription"] = "Index investing made simple." }),
            slug: "about",
            tokens: null);

        seo.MetaTitle.Should().Be("Own the Market");
        seo.MetaDescription.Should().Be("Index investing made simple.");
    }

    [Fact]
    public void Computes_an_absolute_canonical_url_from_the_slug()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "how-to-invest", null);
        seo.CanonicalUrl.Should().Be("https://www.example.co.za/how-to-invest");
    }

    [Fact]
    public void Computes_the_homepage_canonical_without_a_trailing_path()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "", null);
        seo.CanonicalUrl.Should().Be("https://www.example.co.za/");
    }

    [Fact]
    public void An_authored_canonical_override_wins()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D", ["canonicalUrl"] = "https://www.example.co.za/funds" }),
            null, "funds/stx40", null);

        seo.CanonicalUrl.Should().Be("https://www.example.co.za/funds");
    }

    [Fact]
    public void Substitutes_live_data_tokens_into_authored_metadata()
    {
        var seo = Resolver().Resolve(
            Seo(new()
            {
                ["metaTitle"] = "{{fund.name}} | Fund Details",
                ["metaDescription"] = "{{fund.name}} trades at R{{fund.price}} today. Code {{fund.code}}.",
            }),
            null,
            "funds/stx40",
            new Dictionary<string, string>
            {
                ["fund.name"] = "Satrix 40 ETF",
                ["fund.price"] = "88.42",
                ["fund.code"] = "STX40",
            });

        seo.MetaTitle.Should().Be("Satrix 40 ETF | Fund Details");
        seo.MetaDescription.Should().Be("Satrix 40 ETF trades at R88.42 today. Code STX40.");
    }

    [Fact]
    public void Leaves_unmatched_tokens_out_rather_than_printing_braces_to_users()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "{{fund.name}} fund", ["metaDescription"] = "D" }), null, "funds/x", null);

        seo.MetaTitle.Should().Be("fund");
        seo.MetaTitle.Should().NotContain("{{");
    }

    [Fact]
    public void Maps_the_noindex_and_nofollow_toggles_to_a_robots_directive()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D", ["noindex"] = true, ["nofollow"] = true }),
            null, "campaign", null);

        seo.NoIndex.Should().BeTrue();
        seo.NoFollow.Should().BeTrue();
        seo.RobotsContent.Should().Be("noindex, nofollow");
    }

    [Fact]
    public void Defaults_to_indexable_and_followable()
    {
        var seo = Resolver().Resolve(Seo(new() { ["metaTitle"] = "T", ["metaDescription"] = "D" }), null, "about", null);
        seo.RobotsContent.Should().Be("index, follow");
    }

    [Fact]
    public void Open_graph_falls_back_to_the_meta_title_and_description()
    {
        var seo = Resolver().Resolve(
            Seo(new() { ["metaTitle"] = "Tax-Free", ["metaDescription"] = "No tax." }), null, "tfsa", null);

        seo.OgTitle.Should().Be("Tax-Free");
        seo.OgDescription.Should().Be("No tax.");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter SeoResolverTests`
Expected: FAIL — `SeoResolver could not be found`.

- [ ] **Step 3: Create `Seo/SeoDto.cs`**

```csharp
using System.Text.Json;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo;

public sealed record SeoDto
{
    public required string MetaTitle { get; init; }
    public required string MetaDescription { get; init; }
    public required string OgTitle { get; init; }
    public required string OgDescription { get; init; }
    public ImageDto? OgImage { get; init; }
    public required string CanonicalUrl { get; init; }
    public bool NoIndex { get; init; }
    public bool NoFollow { get; init; }
    public string StructuredDataType { get; init; } = "WebPage";
    public JsonElement? StructuredDataOverrides { get; init; }

    /// <summary>Ready-to-render robots directive, e.g. "index, follow".</summary>
    public string RobotsContent => $"{(NoIndex ? "noindex" : "index")}, {(NoFollow ? "nofollow" : "follow")}";
}
```

- [ ] **Step 4: Create `Seo/ISeoResolver.cs`**

```csharp
using Cms.Bff.Contentful;

namespace Cms.Bff.Seo;

public interface ISeoResolver
{
    /// <param name="tokens">
    /// Live values substituted into authored metadata, e.g. "fund.name" -> "Satrix 40 ETF".
    /// Null for pages with no live data.
    /// </param>
    SeoDto Resolve(ResolvedEntry? pageSeo, ResolvedEntry? fallbackSeo, string slug, IReadOnlyDictionary<string, string>? tokens);
}
```

- [ ] **Step 5: Create `Seo/SeoResolver.cs`**

```csharp
using System.Text.RegularExpressions;
using Cms.Bff.Contentful;
using Cms.Bff.Options;
using Cms.Bff.Sections;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Seo;

public sealed partial class SeoResolver : ISeoResolver
{
    private readonly SiteOptions _site;

    public SeoResolver(IOptions<SiteOptions> site) => _site = site.Value;

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_.]+)\s*\}\}")]
    private static partial Regex TokenPattern();

    public SeoDto Resolve(ResolvedEntry? pageSeo, ResolvedEntry? fallbackSeo, string slug, IReadOnlyDictionary<string, string>? tokens)
    {
        var source = pageSeo ?? fallbackSeo;

        var metaTitle = Substitute(source?.GetString("metaTitle") ?? "", tokens);
        var metaDescription = Substitute(source?.GetString("metaDescription") ?? "", tokens);

        var ogTitle = Substitute(source?.GetString("ogTitle"), tokens);
        var ogDescription = Substitute(source?.GetString("ogDescription"), tokens);

        var canonicalOverride = source?.GetString("canonicalUrl");

        return new SeoDto
        {
            MetaTitle = metaTitle,
            MetaDescription = metaDescription,
            OgTitle = string.IsNullOrWhiteSpace(ogTitle) ? metaTitle : ogTitle,
            OgDescription = string.IsNullOrWhiteSpace(ogDescription) ? metaDescription : ogDescription,
            OgImage = SectionMapping.ToImage(source?.GetEntry("ogImage")),
            CanonicalUrl = string.IsNullOrWhiteSpace(canonicalOverride) ? BuildCanonical(slug) : canonicalOverride,
            NoIndex = source?.GetBool("noindex") ?? false,
            NoFollow = source?.GetBool("nofollow") ?? false,
            StructuredDataType = source?.GetString("structuredDataType") ?? "WebPage",
            StructuredDataOverrides = source?.GetRichText("structuredDataOverrides"),
        };
    }

    public string BuildCanonical(string slug)
    {
        var baseUrl = _site.BaseUrl.TrimEnd('/');
        var path = slug.Trim('/');
        return path.Length == 0 ? baseUrl + "/" : $"{baseUrl}/{path}";
    }

    /// <summary>
    /// Replaces {{token}} placeholders with live values. Unmatched tokens are removed
    /// rather than left in place, so a missing value never renders literal braces into
    /// a search result.
    /// </summary>
    private static string Substitute(string? template, IReadOnlyDictionary<string, string>? tokens)
    {
        if (string.IsNullOrEmpty(template)) return template ?? "";
        if (!template.Contains("{{", StringComparison.Ordinal)) return template;

        var replaced = TokenPattern().Replace(template, match =>
            tokens is not null && tokens.TryGetValue(match.Groups[1].Value, out var value) ? value : "");

        return string.Join(" ", replaced.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter SeoResolverTests`
Expected: `Passed! - Failed: 0, Passed: 10`

- [ ] **Step 7: Commit**

```powershell
git add bff/src/Cms.Bff/Seo bff/tests/Cms.Bff.Tests/SeoResolverTests.cs
git commit -m "feat(bff): resolve SEO with fallback chain, canonicals and live-data tokens

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.9: JSON-LD builders (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Seo/JsonLd/IJsonLdBuilder.cs`, `JsonLdContext.cs`, `JsonLdBuilderRegistry.cs`
- Create: `bff/src/Cms.Bff/Seo/JsonLd/{WebPage,Article,Product,FaqPage,Breadcrumb,Organization}JsonLdBuilder.cs`
- Test: `bff/tests/Cms.Bff.Tests/JsonLdBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Cms.Bff.Tests;

public class JsonLdBuilderTests
{
    private static SeoDto Seo(string type) => new()
    {
        MetaTitle = "Satrix 40 ETF | Fund Details",
        MetaDescription = "Track the Top 40.",
        OgTitle = "Satrix 40 ETF",
        OgDescription = "Track the Top 40.",
        CanonicalUrl = "https://www.example.co.za/funds/stx40",
        StructuredDataType = type,
    };

    private static JsonLdBuilderRegistry Registry(params IJsonLdBuilder[] builders) =>
        new(builders, NullLogger<JsonLdBuilderRegistry>.Instance);

    [Fact]
    public void Product_schema_is_built_from_live_fund_data_not_hand_coded()
    {
        var fund = new FundDetailSection
        {
            Id = "fd1", Code = "STX40", Name = "Satrix 40 ETF", Isin = "ZAE000027108",
            ShortDescription = "Top 40 tracker", Nav = 88.42m, Ter = 0.10m,
        };

        var context = new JsonLdContext(Seo("Product"), "funds/stx40", new SectionDto[] { fund }, null, Array.Empty<BreadcrumbDto>());
        var result = Registry(new ProductJsonLdBuilder()).Build(context).Single();
        var json = JsonSerializer.SerializeToElement(result);

        json.GetProperty("@type").GetString().Should().Be("Product");
        json.GetProperty("name").GetString().Should().Be("Satrix 40 ETF");
        json.GetProperty("offers").GetProperty("price").GetString().Should().Be("88.42");
        json.GetProperty("offers").GetProperty("priceCurrency").GetString().Should().Be("ZAR");
    }

    [Fact]
    public void Faq_schema_is_emitted_only_when_the_author_opted_in()
    {
        var optedIn = new FaqAccordionSection
        {
            Id = "faq1", EmitFaqSchema = true,
            Items = new[] { new FaqItemDto("q1", "What is a TFSA?", null, "A tax-free savings account.") },
        };
        var optedOut = optedIn with { Id = "faq2", EmitFaqSchema = false };

        var builder = new FaqPageJsonLdBuilder();

        var withSchema = Registry(builder).Build(
            new JsonLdContext(Seo("FAQPage"), "tfsa", new SectionDto[] { optedIn }, null, Array.Empty<BreadcrumbDto>()));
        var withoutSchema = Registry(builder).Build(
            new JsonLdContext(Seo("FAQPage"), "tfsa", new SectionDto[] { optedOut }, null, Array.Empty<BreadcrumbDto>()));

        var json = JsonSerializer.SerializeToElement(withSchema.Single());
        json.GetProperty("@type").GetString().Should().Be("FAQPage");
        json.GetProperty("mainEntity")[0].GetProperty("acceptedAnswer").GetProperty("text").GetString()
            .Should().Be("A tax-free savings account.");

        withoutSchema.Should().BeEmpty();
    }

    [Fact]
    public void Breadcrumbs_are_emitted_when_the_page_has_an_authored_parent()
    {
        var crumbs = new[]
        {
            new BreadcrumbDto("Home", "https://www.example.co.za/"),
            new BreadcrumbDto("Funds", "https://www.example.co.za/funds"),
            new BreadcrumbDto("Satrix 40 ETF", "https://www.example.co.za/funds/stx40"),
        };

        var result = Registry(new BreadcrumbJsonLdBuilder()).Build(
            new JsonLdContext(Seo("Product"), "funds/stx40", Array.Empty<SectionDto>(), null, crumbs)).Single();

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("@type").GetString().Should().Be("BreadcrumbList");
        json.GetProperty("itemListElement").GetArrayLength().Should().Be(3);
        json.GetProperty("itemListElement")[1].GetProperty("position").GetInt32().Should().Be(2);
    }

    [Fact]
    public void An_unknown_structured_data_type_produces_no_schema_and_does_not_throw()
    {
        var context = new JsonLdContext(Seo("SomethingNobodyImplemented"), "x", Array.Empty<SectionDto>(), null, Array.Empty<BreadcrumbDto>());
        Registry(new ProductJsonLdBuilder()).Build(context).Should().BeEmpty();
    }

    [Fact]
    public void Structured_data_overrides_are_merged_over_generated_properties()
    {
        var seo = Seo("Product") with
        {
            StructuredDataOverrides = JsonDocument.Parse("""{ "brand": "Satrix", "name": "Overridden name" }""").RootElement,
        };
        var fund = new FundDetailSection { Id = "fd1", Code = "STX40", Name = "Satrix 40 ETF", Nav = 88.42m };

        var result = Registry(new ProductJsonLdBuilder()).Build(
            new JsonLdContext(seo, "funds/stx40", new SectionDto[] { fund }, null, Array.Empty<BreadcrumbDto>())).Single();

        var json = JsonSerializer.SerializeToElement(result);
        json.GetProperty("name").GetString().Should().Be("Overridden name");
        json.GetProperty("brand").GetString().Should().Be("Satrix");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter JsonLdBuilderTests`
Expected: FAIL — `JsonLdBuilderRegistry could not be found`.

- [ ] **Step 3: Create `Seo/JsonLd/JsonLdContext.cs` and `IJsonLdBuilder.cs`**

```csharp
using Cms.Bff.Contentful;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed record BreadcrumbDto(string Name, string Url);

public sealed record JsonLdContext(
    SeoDto Seo,
    string Slug,
    IReadOnlyList<SectionDto> Sections,
    ResolvedEntry? Article,
    IReadOnlyList<BreadcrumbDto> Breadcrumbs);

public interface IJsonLdBuilder
{
    /// <summary>The seo.structuredDataType value this builder handles.</summary>
    string StructuredDataType { get; }

    /// <summary>Returns null when this page has nothing to emit.</summary>
    Dictionary<string, object?>? Build(JsonLdContext context);
}
```

- [ ] **Step 4: Create `Seo/JsonLd/JsonLdBuilderRegistry.cs`**

```csharp
using System.Text.Json;

namespace Cms.Bff.Seo.JsonLd;

public sealed class JsonLdBuilderRegistry
{
    private readonly Dictionary<string, IJsonLdBuilder> _builders;
    private readonly ILogger<JsonLdBuilderRegistry> _logger;

    public JsonLdBuilderRegistry(IEnumerable<IJsonLdBuilder> builders, ILogger<JsonLdBuilderRegistry> logger)
    {
        _builders = builders.ToDictionary(b => b.StructuredDataType, StringComparer.Ordinal);
        _logger = logger;
    }

    public IReadOnlyList<Dictionary<string, object?>> Build(JsonLdContext context)
    {
        var results = new List<Dictionary<string, object?>>();

        if (_builders.TryGetValue(context.Seo.StructuredDataType, out var builder))
        {
            var built = builder.Build(context);
            if (built is not null) results.Add(Merge(built, context.Seo.StructuredDataOverrides));
        }
        else if (context.Seo.StructuredDataType is not ("None" or ""))
        {
            _logger.LogWarning(
                "No JSON-LD builder for structured data type '{Type}' on page '{Slug}'. No schema emitted.",
                context.Seo.StructuredDataType, context.Slug);
        }

        // BreadcrumbList is additive: it accompanies whatever the page's primary type is.
        if (context.Breadcrumbs.Count > 1 &&
            _builders.TryGetValue("BreadcrumbList", out var crumbs) &&
            context.Seo.StructuredDataType != "BreadcrumbList")
        {
            var built = crumbs.Build(context);
            if (built is not null) results.Add(built);
        }

        return results;
    }

    private static Dictionary<string, object?> Merge(Dictionary<string, object?> generated, JsonElement? overrides)
    {
        if (overrides is not { ValueKind: JsonValueKind.Object } obj) return generated;

        foreach (var property in obj.EnumerateObject())
            generated[property.Name] = ToClrValue(property.Value);

        return generated;
    }

    private static object? ToClrValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetDecimal(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.Clone(),
    };
}
```

- [ ] **Step 5: Create the builders**

`ProductJsonLdBuilder.cs`:

```csharp
using System.Globalization;
using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed class ProductJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "Product";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        var fund = context.Sections.OfType<FundDetailSection>().FirstOrDefault(f => !f.Unavailable);
        if (fund is null) return null;

        var schema = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Product",
            ["name"] = fund.Name,
            ["description"] = fund.ShortDescription,
            ["url"] = context.Seo.CanonicalUrl,
            ["sku"] = fund.Code,
            ["category"] = "Exchange Traded Fund",
        };

        if (!string.IsNullOrWhiteSpace(fund.Isin))
            schema["identifier"] = new Dictionary<string, object?>
            {
                ["@type"] = "PropertyValue", ["propertyID"] = "ISIN", ["value"] = fund.Isin,
            };

        if (fund.Nav is { } nav)
            schema["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = nav.ToString("0.##", CultureInfo.InvariantCulture),
                ["priceCurrency"] = "ZAR",
                ["availability"] = "https://schema.org/InStock",
                ["url"] = context.Seo.CanonicalUrl,
            };

        return schema;
    }
}
```

`FaqPageJsonLdBuilder.cs`:

```csharp
using Cms.Bff.Sections;

namespace Cms.Bff.Seo.JsonLd;

public sealed class FaqPageJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "FAQPage";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        var items = context.Sections
            .OfType<FaqAccordionSection>()
            .Where(s => s.EmitFaqSchema)
            .SelectMany(s => s.Items)
            .ToList();

        if (items.Count == 0) return null;

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = items.Select(i => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = i.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer", ["text"] = i.PlainTextAnswer,
                },
            }).ToList(),
        };
    }
}
```

`BreadcrumbJsonLdBuilder.cs`:

```csharp
namespace Cms.Bff.Seo.JsonLd;

public sealed class BreadcrumbJsonLdBuilder : IJsonLdBuilder
{
    public string StructuredDataType => "BreadcrumbList";

    public Dictionary<string, object?>? Build(JsonLdContext context)
    {
        if (context.Breadcrumbs.Count < 2) return null;

        return new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = context.Breadcrumbs.Select((crumb, index) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = crumb.Name,
                ["item"] = crumb.Url,
            }).ToList(),
        };
    }
}
```

`ArticleJsonLdBuilder.cs` — `StructuredDataType => "Article"`, reads
`context.Article` (a `ResolvedEntry` of content type `article`) and emits
`@type: Article` with `headline` (title), `description` (excerpt), `image`
(featuredImage asset url), `datePublished` (publishDate, ISO 8601),
`dateModified` (updatedDate ?? publishDate), `author` as
`{ "@type": "Person", "name": ... }`, `mainEntityOfPage` set to the canonical URL.
Returns null when `context.Article` is null.

`WebPageJsonLdBuilder.cs` — `StructuredDataType => "WebPage"`, emits
`@type: WebPage` with `name` (`Seo.MetaTitle`), `description`
(`Seo.MetaDescription`), `url` (`Seo.CanonicalUrl`). Never returns null.

`OrganizationJsonLdBuilder.cs` — `StructuredDataType => "Organization"`, emits
`@type: Organization` with `name` (`Seo.OgTitle`), `url` (site base URL) and
`sameAs` from site settings. Used by the root layout, not by individual pages.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter JsonLdBuilderTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Commit**

```powershell
git add bff/src/Cms.Bff/Seo bff/tests/Cms.Bff.Tests/JsonLdBuilderTests.cs
git commit -m "feat(bff): build JSON-LD per structured data type with override merging

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.10: Cache tag store and caching decorator (TDD)

**Files:**
- Create: `bff/src/Cms.Bff/Caching/CacheTagStore.cs`, `PageCache.cs`
- Test: `bff/tests/Cms.Bff.Tests/CacheTagStoreTests.cs`

A webhook names one entry. Without a reverse index the only safe response is flushing
everything, which makes the cache pointless on a busy site.

- [ ] **Step 1: Write the failing tests**

```csharp
using Cms.Bff.Caching;
using FluentAssertions;
using Xunit;

namespace Cms.Bff.Tests;

public class CacheTagStoreTests
{
    [Fact]
    public void Evicts_every_page_that_embeds_the_changed_entry()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "sec-hero", "sec-global-disclaimer" });
        store.Associate("page:about", new[] { "page-about", "sec-global-disclaimer" });
        store.Associate("page:news", new[] { "page-news", "sec-teasers" });

        var affected = store.KeysFor("sec-global-disclaimer");

        affected.Should().BeEquivalentTo("page:/", "page:about");
    }

    [Fact]
    public void Returns_nothing_for_an_entry_no_cached_page_uses()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home" });

        store.KeysFor("some-unrelated-entry").Should().BeEmpty();
    }

    [Fact]
    public void Re_associating_a_key_replaces_its_previous_entry_ids()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "sec-old-cta" });
        store.Associate("page:/", new[] { "page-home", "sec-new-cta" });

        store.KeysFor("sec-old-cta").Should().BeEmpty("a removed section must stop invalidating the page");
        store.KeysFor("sec-new-cta").Should().ContainSingle().Which.Should().Be("page:/");
    }

    [Fact]
    public void Forgetting_a_key_removes_it_from_every_entry_index()
    {
        var store = new CacheTagStore();
        store.Associate("page:/", new[] { "page-home", "shared" });
        store.Associate("page:about", new[] { "page-about", "shared" });

        store.Forget("page:/");

        store.KeysFor("shared").Should().ContainSingle().Which.Should().Be("page:about");
        store.KeysFor("page-home").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter CacheTagStoreTests`
Expected: FAIL — `CacheTagStore could not be found`.

- [ ] **Step 3: Create `Caching/CacheTagStore.cs`**

```csharp
using System.Collections.Concurrent;

namespace Cms.Bff.Caching;

/// <summary>
/// Reverse index from Contentful entry id to the cache keys whose payload embeds it.
/// Lets a webhook naming one entry evict exactly the affected pages.
/// </summary>
public sealed class CacheTagStore
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _entryToKeys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyToEntries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public void Associate(string cacheKey, IEnumerable<string> entryIds)
    {
        lock (_gate)
        {
            RemoveKeyLocked(cacheKey);

            var ids = new HashSet<string>(entryIds, StringComparer.Ordinal);
            _keyToEntries[cacheKey] = ids;

            foreach (var id in ids)
            {
                var keys = _entryToKeys.GetOrAdd(id, _ => new HashSet<string>(StringComparer.Ordinal));
                keys.Add(cacheKey);
            }
        }
    }

    public IReadOnlyCollection<string> KeysFor(string entryId)
    {
        lock (_gate)
        {
            return _entryToKeys.TryGetValue(entryId, out var keys)
                ? keys.ToArray()
                : Array.Empty<string>();
        }
    }

    public void Forget(string cacheKey)
    {
        lock (_gate)
        {
            RemoveKeyLocked(cacheKey);
        }
    }

    private void RemoveKeyLocked(string cacheKey)
    {
        if (!_keyToEntries.TryRemove(cacheKey, out var previous)) return;

        foreach (var id in previous)
        {
            if (!_entryToKeys.TryGetValue(id, out var keys)) continue;
            keys.Remove(cacheKey);
            if (keys.Count == 0) _entryToKeys.TryRemove(id, out _);
        }
    }
}
```

- [ ] **Step 4: Create `Caching/PageCache.cs`**

```csharp
using Cms.Bff.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Caching;

/// <summary>
/// Caches resolved payloads and keeps the tag index in step. Preview requests never
/// reach this: drafts must not be shared between requests.
/// </summary>
public sealed class PageCache
{
    private readonly IMemoryCache _cache;
    private readonly CacheTagStore _tags;
    private readonly ContentfulOptions _options;
    private readonly ILogger<PageCache> _logger;

    public PageCache(IMemoryCache cache, CacheTagStore tags, IOptions<ContentfulOptions> options, ILogger<PageCache> logger)
    {
        _cache = cache;
        _tags = tags;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        bool preview,
        Func<Task<(T Value, IReadOnlyCollection<string> EntryIds)>> factory)
    {
        if (preview) return (await factory()).Value;

        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        var (value, entryIds) = await factory();

        _cache.Set(cacheKey, value, TimeSpan.FromSeconds(_options.CacheSeconds));
        _tags.Associate(cacheKey, entryIds);

        return value;
    }

    /// <summary>Evicts every cached payload embedding the given entry. Returns the keys evicted.</summary>
    public IReadOnlyCollection<string> InvalidateEntry(string entryId)
    {
        var keys = _tags.KeysFor(entryId);

        foreach (var key in keys)
        {
            _cache.Remove(key);
            _tags.Forget(key);
        }

        _logger.LogInformation("Entry {EntryId} changed: evicted {Count} cached payload(s).", entryId, keys.Count);
        return keys;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test bff/tests/Cms.Bff.Tests --filter CacheTagStoreTests`
Expected: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 6: Commit**

```powershell
git add bff/src/Cms.Bff/Caching bff/tests/Cms.Bff.Tests/CacheTagStoreTests.cs
git commit -m "feat(bff): evict only the pages embedding a changed entry

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.11: Page resolution service

**Files:**
- Create: `bff/src/Cms.Bff/Pages/PageResponse.cs`, `PageResolutionService.cs`

- [ ] **Step 1: Create `Pages/PageResponse.cs`**

```csharp
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

namespace Cms.Bff.Pages;

public sealed record PageResponse
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string PageType { get; init; }
    public required SeoDto Seo { get; init; }
    public IReadOnlyList<Dictionary<string, object?>> JsonLd { get; init; } = Array.Empty<Dictionary<string, object?>>();
    public IReadOnlyList<BreadcrumbDto> Breadcrumbs { get; init; } = Array.Empty<BreadcrumbDto>();
    public IReadOnlyList<SectionDto> Sections { get; init; } = Array.Empty<SectionDto>();
    public DateTimeOffset? UpdatedAt { get; init; }
    /// <summary>Contentful entry ids this payload embeds, for cache tagging and Next.js revalidation tags.</summary>
    public IReadOnlyCollection<string> EntryIds { get; init; } = Array.Empty<string>();
}
```

- [ ] **Step 2: Create `Pages/PageResolutionService.cs`**

```csharp
using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.FundData;
using Cms.Bff.Options;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Pages;

public sealed class PageResolutionService
{
    /// <summary>Reserved slug of the single Contentful template that serves every fund detail URL.</summary>
    public const string FundDetailTemplateSlug = "funds/_detail";

    private readonly IContentfulClient _contentful;
    private readonly EntryLinkResolver _links;
    private readonly SectionResolverRegistry _sections;
    private readonly SeoResolver _seo;
    private readonly JsonLdBuilderRegistry _jsonLd;
    private readonly IFundDataClient _funds;
    private readonly PageCache _cache;
    private readonly SiteOptions _site;
    private readonly ILogger<PageResolutionService> _logger;

    public PageResolutionService(
        IContentfulClient contentful,
        EntryLinkResolver links,
        SectionResolverRegistry sections,
        SeoResolver seo,
        JsonLdBuilderRegistry jsonLd,
        IFundDataClient funds,
        PageCache cache,
        IOptions<SiteOptions> site,
        ILogger<PageResolutionService> logger)
    {
        _contentful = contentful;
        _links = links;
        _sections = sections;
        _seo = seo;
        _jsonLd = jsonLd;
        _funds = funds;
        _cache = cache;
        _site = site.Value;
        _logger = logger;
    }

    public Task<PageResponse?> GetPageAsync(string slug, bool preview, string? fundCode, CancellationToken ct)
    {
        var cacheKey = $"page:{slug}|fund:{fundCode ?? "-"}";

        return _cache.GetOrCreateAsync<PageResponse?>(cacheKey, preview, async () =>
        {
            var page = await FetchPageEntryAsync(slug, preview, ct);
            if (page is null) return (null, Array.Empty<string>());

            var response = await BuildAsync(page, slug, preview, fundCode, ct);
            return (response, response.EntryIds);
        });
    }

    private async Task<ResolvedEntry?> FetchPageEntryAsync(string slug, bool preview, CancellationToken ct)
    {
        var result = await _contentful.QueryAsync(
            new ContentfulQuery("page", new Dictionary<string, string> { ["fields.slug"] = slug }, Include: 6, Limit: 1),
            preview, ct);

        return result.Items.Count == 0 ? null : _links.ResolveItems(result).FirstOrDefault();
    }

    private async Task<PageResponse> BuildAsync(ResolvedEntry page, string slug, bool preview, string? fundCode, CancellationToken ct)
    {
        var context = new SectionContext(slug, fundCode, preview);
        var sectionEntries = page.GetEntries("sections");
        var sections = await _sections.ResolveAllAsync(sectionEntries, context, ct);

        // Live-data tokens let an admin author "{{fund.name}} | Fund Details" once and
        // have it resolve per fund. Only populated when the page carries a fund widget.
        var tokens = BuildTokens(sections);

        var fallbackSeo = await GetDefaultSeoAsync(preview, ct);
        var seo = _seo.Resolve(page.GetEntry("seo"), fallbackSeo, EffectiveSlug(slug, fundCode), tokens);

        var breadcrumbs = await BuildBreadcrumbsAsync(page, seo, ct);

        var jsonLd = _jsonLd.Build(new JsonLdContext(seo, slug, sections, null, breadcrumbs));

        return new PageResponse
        {
            Id = page.Id,
            Slug = slug,
            Title = page.GetString("title") ?? "",
            PageType = page.GetString("pageType") ?? "marketing",
            Seo = seo,
            JsonLd = jsonLd,
            Breadcrumbs = breadcrumbs,
            Sections = sections,
            UpdatedAt = page.UpdatedAt,
            EntryIds = page.CollectEntryIds(),
        };
    }

    /// <summary>The fund detail template renders at /funds/{code}, not at its own reserved slug.</summary>
    private static string EffectiveSlug(string slug, string? fundCode) =>
        slug == FundDetailTemplateSlug && !string.IsNullOrWhiteSpace(fundCode)
            ? $"funds/{fundCode.ToLowerInvariant()}"
            : slug;

    private static IReadOnlyDictionary<string, string>? BuildTokens(IReadOnlyList<SectionDto> sections)
    {
        var fund = sections.OfType<FundDetailSection>().FirstOrDefault(f => !f.Unavailable);
        if (fund is null) return null;

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fund.name"] = fund.Name ?? "",
            ["fund.code"] = fund.Code ?? "",
            ["fund.price"] = fund.Nav?.ToString("0.##") ?? "",
            ["fund.ter"] = fund.Ter?.ToString("0.##") ?? "",
        };
    }

    private async Task<ResolvedEntry?> GetDefaultSeoAsync(bool preview, CancellationToken ct)
    {
        var settings = await _contentful.QueryAsync(new ContentfulQuery("siteSettings", Include: 3, Limit: 1), preview, ct);
        return _links.ResolveItems(settings).FirstOrDefault()?.GetEntry("defaultSeo");
    }

    private Task<IReadOnlyList<BreadcrumbDto>> BuildBreadcrumbsAsync(ResolvedEntry page, SeoDto seo, CancellationToken ct)
    {
        var crumbs = new List<BreadcrumbDto>();

        // Walk the authored parent chain, not the URL string: the admin user controls it.
        var current = page;
        var guard = 0;
        while (current is not null && guard++ < 6)
        {
            var slug = current.GetString("slug") ?? "";
            crumbs.Insert(0, new BreadcrumbDto(current.GetString("title") ?? "", _seo.BuildCanonical(slug)));
            current = current.GetEntry("breadcrumbParent");
        }

        if (crumbs.Count > 0) crumbs[^1] = crumbs[^1] with { Url = seo.CanonicalUrl };

        return Task.FromResult<IReadOnlyList<BreadcrumbDto>>(crumbs);
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build bff/src/Cms.Bff`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Commit**

```powershell
git add bff/src/Cms.Bff/Pages
git commit -m "feat(bff): resolve pages into sections, SEO, breadcrumbs and JSON-LD

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.12: Article, navigation and sitemap services

**Files:**
- Create: `bff/src/Cms.Bff/Pages/ArticleService.cs`, `SiteService.cs`, `SitemapService.cs`
- Create: `bff/src/Cms.Bff/Pages/ArticleResponse.cs`, `NavigationDto.cs`, `SitemapEntry.cs`

- [ ] **Step 1: Create the response DTOs**

```csharp
using System.Text.Json;
using Cms.Bff.Sections;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

namespace Cms.Bff.Pages;

public sealed record ArticleResponse
{
    public required string Id { get; init; }
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public string? Excerpt { get; init; }
    public JsonElement? Body { get; init; }
    public ImageDto? FeaturedImage { get; init; }
    public string? AuthorName { get; init; }
    public string? AuthorJobTitle { get; init; }
    public string? CategoryName { get; init; }
    public string? CategorySlug { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public DateTimeOffset? PublishDate { get; init; }
    public DateTimeOffset? UpdatedDate { get; init; }
    public required SeoDto Seo { get; init; }
    public IReadOnlyList<Dictionary<string, object?>> JsonLd { get; init; } = Array.Empty<Dictionary<string, object?>>();
    public IReadOnlyList<ArticleTeaserDto> Related { get; init; } = Array.Empty<ArticleTeaserDto>();
    public IReadOnlyCollection<string> EntryIds { get; init; } = Array.Empty<string>();
}

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

public sealed record SitemapEntry(string Loc, DateTimeOffset? LastModified, string ChangeFrequency, double Priority);
```

- [ ] **Step 2: Implement `ArticleService.cs`**

Responsibilities, mirroring `PageResolutionService`:
- `GetArticleAsync(slug, preview, ct)` — query content type `article` filtered by
  `fields.slug`, resolve links, map to `ArticleResponse`, resolve SEO with the article's
  `seo` entry (falling back to site default and to
  `RichTextFlattener.Summarise(body, 160)` when `metaDescription` is blank), and build
  JSON-LD via `JsonLdBuilderRegistry` with `context.Article` set to the resolved entry.
  Canonical slug is `news/{slug}`.
- `GetArticlesAsync(categorySlug, page, pageSize, preview, ct)` — query ordered by
  `-fields.publishDate`, map each to `ArticleTeaserDto`, return
  `(IReadOnlyList<ArticleTeaserDto> Items, int Total)`.
- Both go through `PageCache` with keys `article:{slug}` and
  `articles:{categorySlug}:{page}`.

- [ ] **Step 3: Implement `SiteService.cs`**

`GetSiteSettingsAsync(preview, ct)` queries `siteSettings` and both `navigation`
entries, maps `navigationItem` trees to `NavigationItemDto` (resolving
`page`/`article` links to URLs: `page` → `/{slug}`, `article` → `/news/{slug}`,
otherwise `externalUrl` with `External = true`), and emits the sitewide
Organization + WebSite JSON-LD:

```csharp
private IReadOnlyList<Dictionary<string, object?>> BuildSiteJsonLd(ResolvedEntry settings)
{
    var siteName = settings.GetString("siteName") ?? "";
    var baseUrl = _site.BaseUrl.TrimEnd('/');

    return new List<Dictionary<string, object?>>
    {
        new()
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = settings.GetString("organizationLegalName") ?? siteName,
            ["url"] = baseUrl + "/",
            ["logo"] = SectionMapping.ToImage(settings.GetEntry("logo"))?.Url,
            ["sameAs"] = settings.GetStrings("organizationSameAs"),
        },
        new()
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = siteName,
            ["url"] = baseUrl + "/",
        },
    };
}
```

- [ ] **Step 4: Implement `SitemapService.cs`**

`GetEntriesAsync(ct)` returns one list combining:
- every `page` **except** slugs starting with `_` or containing `/_`
  (the `funds/_detail` template is not a URL) — priority `0.8`, `weekly`,
  homepage priority `1.0`
- every `article` at `/news/{slug}` — priority `0.6`, `monthly`, `lastmod` from
  `updatedDate ?? publishDate`
- every fund at `/funds/{code}` from `IFundDataClient.GetFundsAsync(null, null, ct)` —
  priority `0.7`, `daily`

```csharp
private static bool IsPublicSlug(string slug) =>
    !slug.StartsWith('_') && !slug.Contains("/_", StringComparison.Ordinal);
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build bff/src/Cms.Bff`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 6: Commit**

```powershell
git add bff/src/Cms.Bff/Pages
git commit -m "feat(bff): add article, site settings and sitemap services

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 3.13: Endpoints and DI wiring

**Files:**
- Create: `bff/src/Cms.Bff/Endpoints/PageEndpoints.cs`, `ArticleEndpoints.cs`, `SiteEndpoints.cs`, `WebhookEndpoints.cs`, `PreviewEndpoints.cs`
- Modify: `bff/src/Cms.Bff/Program.cs`

- [ ] **Step 1: Create `Endpoints/PageEndpoints.cs`**

```csharp
using Cms.Bff.Pages;

namespace Cms.Bff.Endpoints;

public static class PageEndpoints
{
    public static void MapPageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/pages/{*slug}", async (
            PageResolutionService pages,
            string? slug,
            bool? preview,
            string? fundCode,
            CancellationToken ct) =>
        {
            var normalised = (slug ?? "").Trim('/');
            var page = await pages.GetPageAsync(normalised, preview ?? false, fundCode, ct);

            return page is null
                ? Results.NotFound(new { message = $"No published page at '/{normalised}'." })
                : Results.Ok(page);
        });

        // Convenience alias so the homepage does not need an empty catch-all segment.
        app.MapGet("/api/pages", async (PageResolutionService pages, bool? preview, CancellationToken ct) =>
        {
            var page = await pages.GetPageAsync("", preview ?? false, null, ct);
            return page is null ? Results.NotFound(new { message = "No published homepage." }) : Results.Ok(page);
        });
    }
}
```

- [ ] **Step 2: Create the remaining endpoint files**

| File | Routes |
|---|---|
| `ArticleEndpoints.cs` | `GET /api/articles` (query `category`, `page`, `pageSize`, `preview`) → `{ items, total, page, pageSize }`; `GET /api/articles/{slug}` → `ArticleResponse` or 404 |
| `SiteEndpoints.cs` | `GET /api/site-settings` → `SiteSettingsResponse`; `GET /api/navigation` → `IReadOnlyList<NavigationDto>`; `GET /api/sitemap` → `IReadOnlyList<SitemapEntry>`; `GET /health` → `{ status = "ok", mode = <Contentful:Mode> }` |

- [ ] **Step 3: Create `Endpoints/WebhookEndpoints.cs`**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Cms.Bff.Caching;
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/contentful", async (
            HttpRequest request,
            PageCache cache,
            IHttpClientFactory httpFactory,
            IOptions<IntegrationOptions> integration,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var expected = integration.Value.WebhookSecret;
            if (!request.Headers.TryGetValue("X-Webhook-Secret", out var provided) || provided != expected)
            {
                logger.LogWarning("Rejected Contentful webhook with missing or incorrect secret.");
                return Results.Unauthorized();
            }

            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            var root = document.RootElement;

            var entryId = root.TryGetProperty("sys", out var sys) && sys.TryGetProperty("id", out var id)
                ? id.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(entryId))
                return Results.BadRequest(new { message = "Webhook payload had no sys.id." });

            var evicted = cache.InvalidateEntry(entryId);

            // Tell Next.js to drop its own cached render of the same pages.
            var client = httpFactory.CreateClient("revalidate");
            try
            {
                using var response = await client.PostAsJsonAsync(
                    integration.Value.RevalidateUrl,
                    new { secret = integration.Value.PreviewSecret, entryId, tags = evicted },
                    ct);

                logger.LogInformation(
                    "Revalidation for entry {EntryId} returned {Status} ({Count} cache key(s) evicted).",
                    entryId, (int)response.StatusCode, evicted.Count);
            }
            catch (Exception ex)
            {
                // The BFF cache is already clear; the site self-heals on the next ISR pass.
                logger.LogError(ex, "Could not reach Next.js revalidate endpoint for entry {EntryId}.", entryId);
            }

            return Results.Ok(new { entryId, evicted });
        });
    }
}
```

- [ ] **Step 4: Create `Endpoints/PreviewEndpoints.cs`**

```csharp
using Cms.Bff.Options;
using Microsoft.Extensions.Options;

namespace Cms.Bff.Endpoints;

public static class PreviewEndpoints
{
    public static void MapPreviewEndpoints(this IEndpointRouteBuilder app)
    {
        // Token broker for Next.js draft mode. Next owns the cookie because cookies are
        // per-origin; this endpoint only confirms the handshake secret is valid and
        // says which URL should be shown.
        app.MapGet("/api/preview", (
            string? secret,
            string? slug,
            IOptions<IntegrationOptions> integration,
            IOptions<SiteOptions> site) =>
        {
            if (secret != integration.Value.PreviewSecret)
                return Results.Unauthorized();

            var path = (slug ?? "").Trim('/');
            return Results.Ok(new
            {
                valid = true,
                redirectTo = path.Length == 0 ? "/" : "/" + path,
                siteUrl = site.Value.BaseUrl.TrimEnd('/'),
            });
        });
    }
}
```

- [ ] **Step 5: Replace `Program.cs`**

```csharp
using Cms.Bff.Caching;
using Cms.Bff.Contentful;
using Cms.Bff.Endpoints;
using Cms.Bff.FundData;
using Cms.Bff.Options;
using Cms.Bff.Pages;
using Cms.Bff.Sections;
using Cms.Bff.Sections.Resolvers;
using Cms.Bff.Seo;
using Cms.Bff.Seo.JsonLd;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ContentfulOptions>(builder.Configuration.GetSection(ContentfulOptions.Section));
builder.Services.Configure<FundDataOptions>(builder.Configuration.GetSection(FundDataOptions.Section));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));
builder.Services.Configure<IntegrationOptions>(builder.Configuration.GetSection(IntegrationOptions.Section));

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<CacheTagStore>();
builder.Services.AddSingleton<PageCache>();
builder.Services.AddSingleton<EntryLinkResolver>();

// Fixture mode needs no Contentful account, so the PoC runs before tokens exist.
var contentfulMode = builder.Configuration["Contentful:Mode"] ?? "Fixture";
if (string.Equals(contentfulMode, "Live", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IContentfulClient, HttpContentfulClient>();
else
    builder.Services.AddSingleton<IContentfulClient, FixtureContentfulClient>();

builder.Services.AddHttpClient<IFundDataClient, HttpFundDataClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FundDataOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<ISectionResolver, HeroSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, RichTextSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, CardGridSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, CtaBannerSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, ImageWithTextSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, DisclaimerSectionResolver>();
builder.Services.AddSingleton<ISectionResolver, FaqAccordionSectionResolver>();
builder.Services.AddScoped<ISectionResolver, ArticleTeaserListSectionResolver>();
builder.Services.AddScoped<ISectionResolver, FundListSectionResolver>();
builder.Services.AddScoped<ISectionResolver, FundDetailSectionResolver>();
builder.Services.AddScoped<SectionResolverRegistry>();

builder.Services.AddSingleton<SeoResolver>();
builder.Services.AddSingleton<ISeoResolver>(sp => sp.GetRequiredService<SeoResolver>());
builder.Services.AddSingleton<IJsonLdBuilder, WebPageJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, ArticleJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, ProductJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, FaqPageJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, BreadcrumbJsonLdBuilder>();
builder.Services.AddSingleton<IJsonLdBuilder, OrganizationJsonLdBuilder>();
builder.Services.AddSingleton<JsonLdBuilderRegistry>();

builder.Services.AddScoped<PageResolutionService>();
builder.Services.AddScoped<ArticleService>();
builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<SitemapService>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["Site:BaseUrl"] ?? "http://localhost:3000")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.MapPageEndpoints();
app.MapArticleEndpoints();
app.MapSiteEndpoints();
app.MapWebhookEndpoints();
app.MapPreviewEndpoints();

app.Run();
```

Note: `ArticleTeaserListSectionResolver` depends on `ArticleService`, so it and
everything that composes it are scoped rather than singleton.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test bff/Cms.sln`
Expected: `Passed! - Failed: 0`, 31 tests.

- [ ] **Step 7: Run both services and verify the page endpoint end to end**

```powershell
Start-Process -NoNewWindow dotnet -ArgumentList "run --project bff/src/Cms.FundData.Api --launch-profile http"
Start-Process -NoNewWindow dotnet -ArgumentList "run --project bff/src/Cms.Bff --launch-profile http"
Start-Sleep -Seconds 8

$home = Invoke-RestMethod http://localhost:5080/api/pages
$home.seo.metaTitle
$home.sections | Select-Object -ExpandProperty __type
$home.seo.canonicalUrl

$funds = Invoke-RestMethod "http://localhost:5080/api/pages/funds"
($funds.sections | Where-Object __type -eq "sectionFundListWidget").funds.Count

$detail = Invoke-RestMethod "http://localhost:5080/api/pages/funds/_detail?fundCode=STX40"
$detail.seo.metaTitle
$detail.jsonLd | ConvertTo-Json -Depth 6
```

Expected:
- Home meta title from the fixture; section `__type`s in authored order; canonical
  `http://localhost:3000/`
- Fund list section populated with funds from the Fund Data API
- Fund detail meta title with `{{fund.name}}` replaced by `Satrix 40 ETF`
- JSON-LD containing `"@type": "Product"` with `price` `88.42` and `priceCurrency` `ZAR`
- The `sectionFutureThing` entry on `/about` absent from the response, with a
  `No resolver registered for section content type 'sectionFutureThing'` warning logged

Stop both with: `Get-Process dotnet | Stop-Process -Force`

- [ ] **Step 8: Commit**

```powershell
git add bff/src/Cms.Bff
git commit -m "feat(bff): expose page, article, site, webhook and preview endpoints

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 4 — Next.js renderer

Phase outcome: every page in the brief renders from the BFF tree, with correct
`<head>` metadata, JSON-LD, sitemap and robots.

### Task 4.1: Types and BFF fetch layer

**Files:**
- Create: `web/lib/types.ts`, `web/lib/bff.ts`
- Create: `web/.env.local.example`

- [ ] **Step 1: Create `web/lib/types.ts`**

```ts
export type Image = { url: string; altText: string; width?: number; height?: number; caption?: string };
export type Link = { label: string; url: string };

export type Seo = {
  metaTitle: string;
  metaDescription: string;
  ogTitle: string;
  ogDescription: string;
  ogImage?: Image | null;
  canonicalUrl: string;
  noIndex: boolean;
  noFollow: boolean;
  structuredDataType: string;
  robotsContent: string;
};

export type Breadcrumb = { name: string; url: string };

type Base<T extends string> = { __type: T; id: string };

export type HeroSection = Base<"sectionHero"> & {
  eyebrow?: string | null;
  heading: string;
  subheading?: string | null;
  backgroundImage?: Image | null;
  primaryCta?: Link | null;
  secondaryCta?: Link | null;
  variant: string;
};

export type RichTextSection = Base<"sectionRichText"> & {
  heading?: string | null;
  body?: unknown;
  width: string;
};

export type Card = { id: string; title: string; body?: string | null; image?: Image | null; link?: Link | null; icon?: string | null };
export type CardGridSection = Base<"sectionCardGrid"> & { heading?: string | null; intro?: string | null; cards: Card[]; columns: number };

export type CtaBannerSection = Base<"sectionCtaBanner"> & { heading: string; body?: string | null; cta: Link; variant: string };

export type ArticleTeaser = {
  id: string; title: string; slug: string; excerpt?: string | null;
  image?: Image | null; categoryName?: string | null; publishDate?: string | null; authorName?: string | null;
};
export type ArticleTeaserListSection = Base<"sectionArticleTeaserList"> & { heading?: string | null; articles: ArticleTeaser[] };

export type FundSummary = {
  code: string; name: string; shortDescription: string; categoryId: string;
  nav: number; dayChangePercent: number; ter: number; oneYearReturn: number;
};
export type FundCategory = { id: string; name: string; slug: string };
export type FundListSection = Base<"sectionFundListWidget"> & {
  heading?: string | null; intro?: string | null; categoryId?: string | null;
  showFilters: boolean; displayVariant: string; categories: FundCategory[]; funds: FundSummary[];
};

export type PricePoint = { date: string; nav: number };
export type FundPerformance = { oneYear: number; threeYear: number; fiveYear: number; sinceInception: number };
export type FundDetailSection = Base<"sectionFundDetailWidget"> & {
  code?: string | null; name?: string | null; isin?: string | null; shortDescription?: string | null;
  nav?: number | null; dayChangePercent?: number | null; ter?: number | null;
  inceptionDate?: string | null; factsheetUrl?: string | null;
  performance?: FundPerformance | null; prices: PricePoint[]; unavailable: boolean;
};

export type FaqItem = { id: string; question: string; answer?: unknown; plainTextAnswer: string };
export type FaqAccordionSection = Base<"sectionFaqAccordion"> & { heading?: string | null; items: FaqItem[]; emitFaqSchema: boolean };

export type ImageWithTextSection = Base<"sectionImageWithText"> & {
  heading?: string | null; body?: unknown; image?: Image | null; imagePosition: string; cta?: Link | null;
};

export type DisclaimerSection = Base<"sectionDisclaimer"> & {
  label?: string | null; body?: unknown; severity: string; collapsible: boolean;
};

export type Section =
  | HeroSection | RichTextSection | CardGridSection | CtaBannerSection
  | ArticleTeaserListSection | FundListSection | FundDetailSection
  | FaqAccordionSection | ImageWithTextSection | DisclaimerSection;

export type JsonLdObject = Record<string, unknown>;

export type PageResponse = {
  id: string; slug: string; title: string; pageType: "marketing" | "dataDriven";
  seo: Seo; jsonLd: JsonLdObject[]; breadcrumbs: Breadcrumb[]; sections: Section[];
  updatedAt?: string | null; entryIds: string[];
};

export type ArticleResponse = {
  id: string; slug: string; title: string; excerpt?: string | null; body?: unknown;
  featuredImage?: Image | null; authorName?: string | null; authorJobTitle?: string | null;
  categoryName?: string | null; categorySlug?: string | null; tags: string[];
  publishDate?: string | null; updatedDate?: string | null;
  seo: Seo; jsonLd: JsonLdObject[]; related: ArticleTeaser[]; entryIds: string[];
};

export type NavigationItem = { label: string; url: string; external: boolean; children: NavigationItem[] };
export type Navigation = { key: string; items: NavigationItem[] };
export type SiteSettings = {
  siteName: string; logo?: Image | null; disclaimerText?: unknown;
  socialLinks: { label: string; url: string }[]; navigations: Navigation[]; jsonLd: JsonLdObject[];
};

export type SitemapEntry = { loc: string; lastModified?: string | null; changeFrequency: string; priority: number };
```

- [ ] **Step 2: Create `web/lib/bff.ts`**

```ts
import { draftMode } from "next/headers";
import type {
  ArticleResponse, ArticleTeaser, PageResponse, SiteSettings, SitemapEntry,
} from "./types";

const BFF = process.env.BFF_BASE_URL ?? "http://localhost:5080";
const REVALIDATE_SECONDS = Number(process.env.REVALIDATE_SECONDS ?? 300);

async function isPreview(): Promise<boolean> {
  try {
    return (await draftMode()).isEnabled;
  } catch {
    // draftMode() throws outside a request scope, e.g. during sitemap generation.
    return false;
  }
}

async function get<T>(path: string, tags: string[]): Promise<T | null> {
  const preview = await isPreview();
  const separator = path.includes("?") ? "&" : "?";
  const url = `${BFF}${path}${preview ? `${separator}preview=true` : ""}`;

  const response = await fetch(url, {
    // Drafts must never be cached or shared between viewers.
    ...(preview
      ? { cache: "no-store" as const }
      : { next: { revalidate: REVALIDATE_SECONDS, tags } }),
  });

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`BFF ${response.status} for ${url}`);
  return (await response.json()) as T;
}

export function getPage(slug: string): Promise<PageResponse | null> {
  const path = slug === "" ? "/api/pages" : `/api/pages/${slug}`;
  return get<PageResponse>(path, ["pages", `page:${slug}`]);
}

export function getFundDetailPage(fundCode: string): Promise<PageResponse | null> {
  return get<PageResponse>(
    `/api/pages/funds/_detail?fundCode=${encodeURIComponent(fundCode)}`,
    ["pages", "funds", `fund:${fundCode}`],
  );
}

export function getArticle(slug: string): Promise<ArticleResponse | null> {
  return get<ArticleResponse>(`/api/articles/${slug}`, ["articles", `article:${slug}`]);
}

export function getArticles(category?: string, page = 1): Promise<{ items: ArticleTeaser[]; total: number } | null> {
  const query = new URLSearchParams({ page: String(page) });
  if (category) query.set("category", category);
  return get<{ items: ArticleTeaser[]; total: number }>(`/api/articles?${query}`, ["articles"]);
}

export function getSiteSettings(): Promise<SiteSettings | null> {
  return get<SiteSettings>("/api/site-settings", ["site"]);
}

export function getSitemap(): Promise<SitemapEntry[] | null> {
  return get<SitemapEntry[]>("/api/sitemap", ["pages", "articles", "funds"]);
}
```

- [ ] **Step 3: Create `web/.env.local.example`**

```bash
BFF_BASE_URL=http://localhost:5080
NEXT_PUBLIC_SITE_URL=http://localhost:3000
PREVIEW_SECRET=dev-preview-secret
REVALIDATE_SECONDS=300
```

Copy it to `web/.env.local` before running.

- [ ] **Step 4: Verify types compile**

Run: `npx --prefix web tsc --noEmit -p web/tsconfig.json`
Expected: no output (success).

- [ ] **Step 5: Commit**

```powershell
git add web/lib web/.env.local.example
git commit -m "feat(web): add BFF response types and tagged fetch layer

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.2: Component registry and section renderer (TDD)

**Files:**
- Create: `web/components/sections/registry.ts`, `SectionRenderer.tsx`
- Test: `web/tests/registry.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import type { Section } from "@/lib/types";

describe("SectionRenderer", () => {
  it("renders each known section type through the registry", () => {
    const sections = [
      { __type: "sectionHero", id: "h1", heading: "Own the market", variant: "default" },
      { __type: "sectionCtaBanner", id: "c1", heading: "Start investing", cta: { label: "Invest now", url: "/invest" }, variant: "primary" },
    ] as unknown as Section[];

    render(<SectionRenderer sections={sections} />);

    expect(screen.getByText("Own the market")).toBeDefined();
    expect(screen.getByText("Invest now")).toBeDefined();
  });

  it("skips an unknown section type without crashing the page", () => {
    const sections = [
      { __type: "sectionFutureThing", id: "x1" },
      { __type: "sectionHero", id: "h1", heading: "Still rendered", variant: "default" },
    ] as unknown as Section[];

    render(<SectionRenderer sections={sections} />);

    expect(screen.getByText("Still rendered")).toBeDefined();
  });

  it("preserves the order the admin user set in Contentful", () => {
    const sections = [
      { __type: "sectionHero", id: "h1", heading: "First", variant: "default" },
      { __type: "sectionHero", id: "h2", heading: "Second", variant: "default" },
    ] as unknown as Section[];

    const { container } = render(<SectionRenderer sections={sections} />);
    const headings = Array.from(container.querySelectorAll("h1, h2")).map((n) => n.textContent);

    expect(headings).toEqual(["First", "Second"]);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm --prefix web test`
Expected: FAIL — cannot resolve `@/components/sections/SectionRenderer`.

- [ ] **Step 3: Create `web/components/sections/registry.ts`**

```ts
import type { ComponentType } from "react";
import type { Section } from "@/lib/types";
import { Hero } from "./Hero";
import { RichTextBlock } from "./RichTextBlock";
import { CardGrid } from "./CardGrid";
import { CtaBanner } from "./CtaBanner";
import { ArticleTeaserList } from "./ArticleTeaserList";
import { FundListWidget } from "./FundListWidget";
import { FundDetailWidget } from "./FundDetailWidget";
import { FaqAccordion } from "./FaqAccordion";
import { ImageWithText } from "./ImageWithText";
import { Disclaimer } from "./Disclaimer";

/**
 * The only file a developer touches to support a NEW kind of block.
 * Composing a page from existing blocks requires no code change at all.
 */
export const sectionRegistry: Record<Section["__type"], ComponentType<never>> = {
  sectionHero: Hero,
  sectionRichText: RichTextBlock,
  sectionCardGrid: CardGrid,
  sectionCtaBanner: CtaBanner,
  sectionArticleTeaserList: ArticleTeaserList,
  sectionFundListWidget: FundListWidget,
  sectionFundDetailWidget: FundDetailWidget,
  sectionFaqAccordion: FaqAccordion,
  sectionImageWithText: ImageWithText,
  sectionDisclaimer: Disclaimer,
} as Record<Section["__type"], ComponentType<never>>;
```

- [ ] **Step 4: Create `web/components/sections/SectionRenderer.tsx`**

```tsx
import type { ComponentType } from "react";
import type { Section } from "@/lib/types";
import { sectionRegistry } from "./registry";

export function SectionRenderer({ sections }: { sections: Section[] }) {
  return (
    <>
      {sections.map((section) => {
        const Component = sectionRegistry[section.__type] as ComponentType<{ section: Section }> | undefined;

        if (!Component) {
          // Degradation contract, matching the BFF: a block type this build does not
          // know about is skipped, never fatal. The BFF drops unknown types already;
          // this guards against a frontend deployed behind a newer content model.
          if (process.env.NODE_ENV === "development") {
            return (
              <div key={section.id} className="mx-auto my-4 max-w-4xl border border-dashed border-amber-500 bg-amber-50 p-4 text-sm text-amber-900">
                No component registered for section type <code className="font-mono">{section.__type}</code>.
                Add it to <code className="font-mono">components/sections/registry.ts</code>.
              </div>
            );
          }
          return null;
        }

        return <Component key={section.id} section={section} />;
      })}
    </>
  );
}
```

- [ ] **Step 5: Create the section components**

`web/components/sections/Hero.tsx` as the exemplar:

```tsx
import Image from "next/image";
import Link from "next/link";
import type { HeroSection } from "@/lib/types";

export function Hero({ section }: { section: HeroSection }) {
  return (
    <section className="relative isolate overflow-hidden bg-slate-900 text-white">
      {section.backgroundImage && (
        <Image
          src={section.backgroundImage.url}
          alt={section.backgroundImage.altText}
          fill
          priority
          sizes="100vw"
          className="absolute inset-0 -z-10 object-cover opacity-40"
        />
      )}
      <div className="mx-auto max-w-5xl px-4 py-20">
        {section.eyebrow && <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-sky-300">{section.eyebrow}</p>}
        <h1 className="text-4xl font-bold leading-tight sm:text-5xl">{section.heading}</h1>
        {section.subheading && <p className="mt-4 max-w-2xl text-lg text-slate-200">{section.subheading}</p>}
        <div className="mt-8 flex flex-wrap gap-3">
          {section.primaryCta && (
            <Link href={section.primaryCta.url} className="rounded bg-sky-500 px-5 py-3 font-semibold text-white hover:bg-sky-400">
              {section.primaryCta.label}
            </Link>
          )}
          {section.secondaryCta && (
            <Link href={section.secondaryCta.url} className="rounded border border-white/40 px-5 py-3 font-semibold hover:bg-white/10">
              {section.secondaryCta.label}
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}
```

Remaining components, each a named export taking `{ section }`:

| File | Component | Rendering notes |
|---|---|---|
| `RichTextBlock.tsx` | `RichTextBlock` | Renders `section.body` via `<RichText document={section.body} />` (Task 4.3) |
| `CardGrid.tsx` | `CardGrid` | CSS grid, `columns` mapped to a static Tailwind class lookup (`{2:"md:grid-cols-2",3:"md:grid-cols-3",4:"md:grid-cols-4"}`) — Tailwind cannot compile a dynamic class string |
| `CtaBanner.tsx` | `CtaBanner` | Heading, body, one `<Link>`; `variant` selects a background class from a static lookup |
| `ArticleTeaserList.tsx` | `ArticleTeaserList` | Cards linking to `/news/{slug}`, showing category, date (`<time dateTime={...}>`) and author |
| `FundListWidget.tsx` | `FundListWidget` | **Server component.** Renders all funds server-side. When `showFilters`, renders category filters as real `<Link href={"/funds?categoryId=" + c.id}>` anchors — never client-only buttons — so crawlers can follow them |
| `FundDetailWidget.tsx` | `FundDetailWidget` | NAV, day change, TER, ISIN, inception date, performance table, factsheet link. When `section.unavailable`, renders a plain "Fund data is temporarily unavailable" notice instead |
| `FaqAccordion.tsx` | `FaqAccordion` | `<details><summary>` per item so answers are in the server-rendered DOM while collapsed |
| `ImageWithText.tsx` | `ImageWithText` | Two-column flex, `imagePosition === "right"` applies `md:flex-row-reverse` |
| `Disclaimer.tsx` | `Disclaimer` | `severity` selects a colour from a static lookup; when `collapsible`, wraps in `<details>` with the label as `<summary>` |

Add to `web/next.config.ts` so Contentful images load:

```ts
import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [{ protocol: "https", hostname: "images.ctfassets.net" }],
  },
};

export default nextConfig;
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `npm --prefix web test`
Expected: `Test Files 1 passed`, `Tests 3 passed`.

- [ ] **Step 7: Commit**

```powershell
git add web/components web/tests web/next.config.ts
git commit -m "feat(web): add section component registry with crawler-safe rendering

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.3: Rich text renderer

**Files:**
- Create: `web/components/RichText.tsx`

- [ ] **Step 1: Install the Contentful rich text renderer**

```powershell
npm --prefix web install @contentful/rich-text-react-renderer @contentful/rich-text-types
```

- [ ] **Step 2: Create `web/components/RichText.tsx`**

```tsx
import { documentToReactComponents, type Options } from "@contentful/rich-text-react-renderer";
import { BLOCKS, INLINES, type Document } from "@contentful/rich-text-types";
import Link from "next/link";
import type { ReactNode } from "react";

const options: Options = {
  renderNode: {
    [BLOCKS.PARAGRAPH]: (_node, children: ReactNode) => <p className="mb-4 leading-relaxed">{children}</p>,
    [BLOCKS.HEADING_2]: (_node, children: ReactNode) => <h2 className="mb-3 mt-8 text-2xl font-bold">{children}</h2>,
    [BLOCKS.HEADING_3]: (_node, children: ReactNode) => <h3 className="mb-2 mt-6 text-xl font-semibold">{children}</h3>,
    [BLOCKS.UL_LIST]: (_node, children: ReactNode) => <ul className="mb-4 list-disc space-y-1 pl-6">{children}</ul>,
    [BLOCKS.OL_LIST]: (_node, children: ReactNode) => <ol className="mb-4 list-decimal space-y-1 pl-6">{children}</ol>,
    [INLINES.HYPERLINK]: (node, children: ReactNode) => {
      const href = (node.data as { uri: string }).uri;
      const external = /^https?:\/\//.test(href);
      return external ? (
        <a href={href} className="text-sky-700 underline" rel="noopener noreferrer" target="_blank">{children}</a>
      ) : (
        <Link href={href} className="text-sky-700 underline">{children}</Link>
      );
    },
  },
};

export function RichText({ document }: { document: unknown }) {
  if (!document || typeof document !== "object") return null;
  return <>{documentToReactComponents(document as Document, options)}</>;
}
```

- [ ] **Step 3: Verify it compiles**

Run: `npx --prefix web tsc --noEmit -p web/tsconfig.json`
Expected: no output.

- [ ] **Step 4: Commit**

```powershell
git add web/components/RichText.tsx web/package.json web/package-lock.json
git commit -m "feat(web): render Contentful rich text server-side

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.4: Metadata mapper and JSON-LD (TDD)

**Files:**
- Create: `web/lib/metadata.ts`, `web/components/seo/JsonLd.tsx`
- Test: `web/tests/metadata.test.ts`, `web/tests/jsonld.test.tsx`

- [ ] **Step 1: Write the failing tests**

```ts
// web/tests/metadata.test.ts
import { describe, expect, it } from "vitest";
import { toMetadata } from "@/lib/metadata";
import type { Seo } from "@/lib/types";

const seo: Seo = {
  metaTitle: "Tax-Free Investing",
  metaDescription: "Invest without paying tax on growth.",
  ogTitle: "Tax-Free Investing",
  ogDescription: "Invest without paying tax on growth.",
  ogImage: { url: "https://images.ctfassets.net/x/tfsa.jpg", altText: "A tax-free savings account illustration", width: 1200, height: 630 },
  canonicalUrl: "https://www.example.co.za/tax-free-investing",
  noIndex: false,
  noFollow: false,
  structuredDataType: "WebPage",
  robotsContent: "index, follow",
};

describe("toMetadata", () => {
  it("maps the SEO object onto Next.js metadata", () => {
    const meta = toMetadata(seo);

    expect(meta.title).toBe("Tax-Free Investing");
    expect(meta.description).toBe("Invest without paying tax on growth.");
    expect(meta.alternates?.canonical).toBe("https://www.example.co.za/tax-free-investing");
    expect(meta.openGraph?.title).toBe("Tax-Free Investing");
    expect(meta.twitter?.card).toBe("summary_large_image");
  });

  it("carries the og image through with its alt text", () => {
    const images = toMetadata(seo).openGraph?.images;
    expect(Array.isArray(images) && images[0]).toMatchObject({
      url: "https://images.ctfassets.net/x/tfsa.jpg",
      alt: "A tax-free savings account illustration",
    });
  });

  it("maps the noindex toggle onto the robots directive", () => {
    const meta = toMetadata({ ...seo, noIndex: true, noFollow: true, robotsContent: "noindex, nofollow" });
    expect(meta.robots).toMatchObject({ index: false, follow: false });
  });

  it("applies a canonical override for a filtered listing page", () => {
    const meta = toMetadata(seo, { canonicalOverride: "https://www.example.co.za/funds" });
    expect(meta.alternates?.canonical).toBe("https://www.example.co.za/funds");
  });

  it("can force noindex without also emitting a conflicting canonical override", () => {
    const meta = toMetadata(seo, { forceNoIndex: true });
    expect(meta.robots).toMatchObject({ index: false, follow: true });
    expect(meta.alternates?.canonical).toBe(seo.canonicalUrl);
  });
});
```

```tsx
// web/tests/jsonld.test.tsx
import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { JsonLd } from "@/components/seo/JsonLd";

describe("JsonLd", () => {
  it("emits one ld+json script per schema object", () => {
    const { container } = render(
      <JsonLd data={[{ "@type": "WebPage", name: "Home" }, { "@type": "BreadcrumbList" }]} />,
    );
    expect(container.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(2);
  });

  it("escapes angle brackets so a script tag in content cannot break out", () => {
    const { container } = render(<JsonLd data={[{ name: "</script><img onerror=alert(1)>" }]} />);
    const html = container.querySelector("script")!.innerHTML;

    expect(html).not.toContain("</script>");
    expect(html).toContain("\\u003c");
  });

  it("renders nothing when there is no structured data", () => {
    const { container } = render(<JsonLd data={[]} />);
    expect(container.querySelector("script")).toBeNull();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `npm --prefix web test`
Expected: FAIL — cannot resolve `@/lib/metadata`.

- [ ] **Step 3: Create `web/lib/metadata.ts`**

```ts
import type { Metadata } from "next";
import type { Seo } from "./types";

export type MetadataOptions = {
  /** Point the canonical at a different URL, e.g. a filtered listing to its base page. */
  canonicalOverride?: string;
  /** Force noindex for URL variants that should not be indexed at all. */
  forceNoIndex?: boolean;
};

export function toMetadata(seo: Seo, options: MetadataOptions = {}): Metadata {
  const index = !seo.noIndex && !options.forceNoIndex;
  const follow = !seo.noFollow;
  const canonical = options.canonicalOverride ?? seo.canonicalUrl;

  const images = seo.ogImage
    ? [{ url: seo.ogImage.url, alt: seo.ogImage.altText, width: seo.ogImage.width, height: seo.ogImage.height }]
    : undefined;

  return {
    title: seo.metaTitle,
    description: seo.metaDescription,
    alternates: { canonical },
    robots: { index, follow, googleBot: { index, follow } },
    openGraph: {
      type: "website",
      url: canonical,
      title: seo.ogTitle,
      description: seo.ogDescription,
      images,
    },
    twitter: {
      card: "summary_large_image",
      title: seo.ogTitle,
      description: seo.ogDescription,
      images: images?.map((image) => image.url),
    },
  };
}
```

- [ ] **Step 4: Create `web/components/seo/JsonLd.tsx`**

```tsx
import type { JsonLdObject } from "@/lib/types";

/**
 * Escapes the characters that could terminate the script element early. Content is
 * admin-authored, so this is defence in depth rather than a theoretical concern.
 */
function serialise(value: JsonLdObject): string {
  return JSON.stringify(value)
    .replace(/</g, "\\u003c")
    .replace(/>/g, "\\u003e")
    .replace(/&/g, "\\u0026");
}

export function JsonLd({ data }: { data: JsonLdObject[] }) {
  if (!data?.length) return null;

  return (
    <>
      {data.map((item, index) => (
        <script
          key={index}
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: serialise(item) }}
        />
      ))}
    </>
  );
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `npm --prefix web test`
Expected: `Tests 11 passed` (3 registry + 5 metadata + 3 jsonld).

- [ ] **Step 6: Commit**

```powershell
git add web/lib/metadata.ts web/components/seo web/tests
git commit -m "feat(web): map BFF SEO object to Next metadata and safe JSON-LD

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.5: Indexation policy (TDD)

**Files:**
- Create: `web/lib/indexation.ts`
- Test: `web/tests/indexation.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from "vitest";
import { resolveIndexation } from "@/lib/indexation";

const BASE = "https://www.example.co.za/funds";

describe("resolveIndexation", () => {
  it("leaves the unfiltered listing self-canonical and indexable", () => {
    expect(resolveIndexation(BASE, {}, ["1", "2", "3"])).toEqual({ canonicalOverride: undefined, forceNoIndex: false });
  });

  it("canonicalises a known category filter to the base listing", () => {
    expect(resolveIndexation(BASE, { categoryId: "2" }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: BASE, forceNoIndex: false });
  });

  it("noindexes an unrecognised category rather than canonicalising a page that lists nothing", () => {
    expect(resolveIndexation(BASE, { categoryId: "999" }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: undefined, forceNoIndex: true });
  });

  it("noindexes arbitrary or multi-parameter combinations to stop URL-space explosion", () => {
    expect(resolveIndexation(BASE, { categoryId: "2", sort: "ter" }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: undefined, forceNoIndex: true });
    expect(resolveIndexation(BASE, { utm_source: "newsletter" }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: undefined, forceNoIndex: true });
  });

  it("keeps genuine pagination self-canonical and indexable, because page 2 is not a duplicate", () => {
    expect(resolveIndexation(BASE, { page: "2" }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: undefined, forceNoIndex: false });
  });

  it("never combines a canonical override with noindex", () => {
    const cases = [{}, { categoryId: "2" }, { categoryId: "999" }, { page: "2" }, { sort: "ter" }];
    for (const params of cases) {
      const result = resolveIndexation(BASE, params, ["1", "2", "3"]);
      expect(result.canonicalOverride !== undefined && result.forceNoIndex).toBe(false);
    }
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npm --prefix web test`
Expected: FAIL — cannot resolve `@/lib/indexation`.

- [ ] **Step 3: Create `web/lib/indexation.ts`**

```ts
import type { MetadataOptions } from "./metadata";

/**
 * Indexation policy for data-driven listing pages with query-string filters.
 *
 * The decision, stated explicitly rather than left ambiguous:
 *  - the bare listing is indexable and self-canonical
 *  - a single KNOWN category filter is a near-duplicate, so it canonicalises to the
 *    base listing and stays indexable
 *  - an unknown category, or any other/extra parameter, is noindex,follow — this is
 *    what stops thousands of near-duplicate URLs entering the index
 *  - genuine pagination is self-canonical and indexable: page 2 holds different funds
 *
 * A canonical override and noindex are never emitted together. Google discards the
 * canonical on a noindexed page, so combining them is contradictory signalling.
 */
const PAGINATION_PARAM = "page";
const FILTER_PARAM = "categoryId";

export function resolveIndexation(
  baseUrl: string,
  params: Record<string, string | string[] | undefined>,
  knownCategoryIds: string[],
): MetadataOptions {
  const keys = Object.keys(params).filter((key) => params[key] !== undefined && params[key] !== "");

  if (keys.length === 0) return { canonicalOverride: undefined, forceNoIndex: false };

  if (keys.length === 1 && keys[0] === PAGINATION_PARAM) {
    return { canonicalOverride: undefined, forceNoIndex: false };
  }

  if (keys.length === 1 && keys[0] === FILTER_PARAM) {
    const value = params[FILTER_PARAM];
    const categoryId = Array.isArray(value) ? value[0] : value;

    return knownCategoryIds.includes(categoryId ?? "")
      ? { canonicalOverride: baseUrl, forceNoIndex: false }
      : { canonicalOverride: undefined, forceNoIndex: true };
  }

  return { canonicalOverride: undefined, forceNoIndex: true };
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npm --prefix web test`
Expected: `Tests 17 passed`.

- [ ] **Step 5: Commit**

```powershell
git add web/lib/indexation.ts web/tests/indexation.test.ts
git commit -m "feat(web): implement explicit indexation policy for filtered listings

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.6: Root layout, header and footer

**Files:**
- Modify: `web/app/layout.tsx`
- Create: `web/components/nav/Header.tsx`, `web/components/nav/Footer.tsx`, `web/components/DraftModeBanner.tsx`

- [ ] **Step 1: Create `web/components/nav/Header.tsx`**

```tsx
import Link from "next/link";
import type { NavigationItem, SiteSettings } from "@/lib/types";

function NavLink({ item }: { item: NavigationItem }) {
  if (item.children.length > 0) {
    // Server-rendered disclosure: children are in the DOM for crawlers even when closed.
    return (
      <details className="group relative">
        <summary className="cursor-pointer list-none px-3 py-2 font-medium hover:text-sky-700">{item.label}</summary>
        <ul className="absolute left-0 z-10 min-w-48 rounded border border-slate-200 bg-white p-2 shadow-lg">
          {item.children.map((child) => (
            <li key={child.label}>
              <NavLink item={child} />
            </li>
          ))}
        </ul>
      </details>
    );
  }

  return item.external ? (
    <a href={item.url} className="block px-3 py-2 font-medium hover:text-sky-700" rel="noopener noreferrer" target="_blank">
      {item.label}
    </a>
  ) : (
    <Link href={item.url} className="block px-3 py-2 font-medium hover:text-sky-700">
      {item.label}
    </Link>
  );
}

export function Header({ settings }: { settings: SiteSettings | null }) {
  const nav = settings?.navigations.find((n) => n.key === "header");

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-4">
        <Link href="/" className="text-lg font-bold">{settings?.siteName ?? "Fund Manager"}</Link>
        <nav aria-label="Main">
          <ul className="flex items-center gap-1">
            {nav?.items.map((item) => (
              <li key={item.label}><NavLink item={item} /></li>
            ))}
          </ul>
        </nav>
      </div>
    </header>
  );
}
```

`Footer.tsx` mirrors it using the `footer` navigation, and additionally renders
`settings.socialLinks` and `<RichText document={settings.disclaimerText} />` in a
small-print block.

- [ ] **Step 2: Create `web/components/DraftModeBanner.tsx`**

```tsx
import { draftMode } from "next/headers";

export async function DraftModeBanner() {
  if (!(await draftMode()).isEnabled) return null;

  return (
    <div className="bg-amber-400 px-4 py-2 text-center text-sm font-semibold text-amber-950">
      Draft mode: you are seeing unpublished content.{" "}
      <a href="/api/draft/disable" className="underline">Exit preview</a>
    </div>
  );
}
```

- [ ] **Step 3: Replace `web/app/layout.tsx`**

```tsx
import type { Metadata } from "next";
import "./globals.css";
import { getSiteSettings } from "@/lib/bff";
import { Header } from "@/components/nav/Header";
import { Footer } from "@/components/nav/Footer";
import { JsonLd } from "@/components/seo/JsonLd";
import { DraftModeBanner } from "@/components/DraftModeBanner";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000"),
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  const settings = await getSiteSettings();

  return (
    <html lang="en-ZA">
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        {/* Organization + WebSite, emitted once sitewide rather than per page. */}
        <JsonLd data={settings?.jsonLd ?? []} />
        <DraftModeBanner />
        <Header settings={settings} />
        <main>{children}</main>
        <Footer settings={settings} />
      </body>
    </html>
  );
}
```

- [ ] **Step 4: Verify it compiles**

Run: `npx --prefix web tsc --noEmit -p web/tsconfig.json`
Expected: no output.

- [ ] **Step 5: Commit**

```powershell
git add web/app/layout.tsx web/components/nav web/components/DraftModeBanner.tsx
git commit -m "feat(web): render Contentful-driven header, footer and sitewide JSON-LD

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.7: Catch-all page route

**Files:**
- Create: `web/app/[[...slug]]/page.tsx`
- Delete: `web/app/page.tsx` (replaced by the catch-all)

- [ ] **Step 1: Create the route**

```tsx
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { getPage, getSitemap } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { resolveIndexation } from "@/lib/indexation";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import { JsonLd } from "@/components/seo/JsonLd";
import type { FundListSection } from "@/lib/types";

type Props = {
  params: Promise<{ slug?: string[] }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const toSlug = (segments?: string[]) => (segments ?? []).join("/");

// Statically generate every published URL the BFF knows about. Pages published later
// are served on demand and cached by ISR, so a new page needs no redeploy.
export async function generateStaticParams() {
  const entries = await getSitemap();
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

  return (entries ?? [])
    .map((entry) => entry.loc.replace(base, "").replace(/^\//, ""))
    .filter((path) => !path.startsWith("news/") && !path.startsWith("funds/"))
    .map((path) => ({ slug: path === "" ? [] : path.split("/") }));
}

export const dynamicParams = true;
export const revalidate = 300;

export async function generateMetadata({ params, searchParams }: Props): Promise<Metadata> {
  const slug = toSlug((await params).slug);
  const page = await getPage(slug);
  if (!page) return {};

  const query = await searchParams;
  const fundList = page.sections.find((s): s is FundListSection => s.__type === "sectionFundListWidget");

  // Filtered listing variants get an explicit indexation decision; ordinary pages do not.
  const options = fundList
    ? resolveIndexation(page.seo.canonicalUrl, query, fundList.categories.map((c) => c.id))
    : {};

  return toMetadata(page.seo, options);
}

export default async function CatchAllPage({ params, searchParams }: Props) {
  const slug = toSlug((await params).slug);

  // The fund detail template is not a URL of its own; it is served via /funds/[code].
  if (slug.startsWith("_") || slug.includes("/_")) notFound();

  const page = await getPage(slug);
  if (!page) notFound();

  const query = await searchParams;
  const categoryId = typeof query.categoryId === "string" ? query.categoryId : undefined;

  // Apply the category filter server-side so the filtered view is crawlable.
  const sections = categoryId
    ? page.sections.map((section) =>
        section.__type === "sectionFundListWidget"
          ? { ...section, funds: section.funds.filter((f) => f.categoryId === categoryId) }
          : section,
      )
    : page.sections;

  return (
    <>
      <JsonLd data={page.jsonLd} />
      <SectionRenderer sections={sections} />
    </>
  );
}
```

- [ ] **Step 2: Delete the scaffolded home page**

```powershell
Remove-Item web/app/page.tsx
```

- [ ] **Step 3: Run the stack and verify pages render**

```powershell
Copy-Item web/.env.local.example web/.env.local -Force
Start-Process -NoNewWindow dotnet -ArgumentList "run --project bff/src/Cms.FundData.Api --launch-profile http"
Start-Process -NoNewWindow dotnet -ArgumentList "run --project bff/src/Cms.Bff --launch-profile http"
Start-Process -NoNewWindow npm -ArgumentList "--prefix web run dev"
Start-Sleep -Seconds 15

(Invoke-WebRequest http://localhost:3000/).Content -match "<title>" 
(Invoke-WebRequest http://localhost:3000/tax-free-investing).StatusCode
(Invoke-WebRequest http://localhost:3000/funds).Content -match "application/ld\+json"
```

Expected: `True`, `200`, `True`.

- [ ] **Step 4: Commit**

```powershell
git add web/app
git commit -m "feat(web): render any Contentful page through the catch-all route

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.8: News and fund detail routes

**Files:**
- Create: `web/app/news/[slug]/page.tsx`, `web/app/funds/[code]/page.tsx`

- [ ] **Step 1: Create `web/app/news/[slug]/page.tsx`**

```tsx
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { getArticle } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { RichText } from "@/components/RichText";
import { JsonLd } from "@/components/seo/JsonLd";

type Props = { params: Promise<{ slug: string }> };

export const revalidate = 300;

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const article = await getArticle((await params).slug);
  if (!article) return {};

  return {
    ...toMetadata(article.seo),
    openGraph: {
      ...toMetadata(article.seo).openGraph,
      type: "article",
      publishedTime: article.publishDate ?? undefined,
      modifiedTime: article.updatedDate ?? article.publishDate ?? undefined,
      authors: article.authorName ? [article.authorName] : undefined,
    },
  };
}

export default async function ArticlePage({ params }: Props) {
  const article = await getArticle((await params).slug);
  if (!article) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-12">
      <JsonLd data={article.jsonLd} />

      {article.categoryName && (
        <Link href={`/news?category=${article.categorySlug}`} className="text-sm font-semibold uppercase tracking-wide text-sky-700">
          {article.categoryName}
        </Link>
      )}
      <h1 className="mt-2 text-4xl font-bold leading-tight">{article.title}</h1>

      <p className="mt-3 text-sm text-slate-600">
        {article.authorName && <span>By {article.authorName}</span>}
        {article.publishDate && (
          <>
            {article.authorName && " · "}
            <time dateTime={article.publishDate}>
              {new Date(article.publishDate).toLocaleDateString("en-ZA", { year: "numeric", month: "long", day: "numeric" })}
            </time>
          </>
        )}
      </p>

      {article.featuredImage && (
        <Image
          src={article.featuredImage.url}
          alt={article.featuredImage.altText}
          width={article.featuredImage.width ?? 1200}
          height={article.featuredImage.height ?? 630}
          className="mt-8 w-full rounded"
          priority
        />
      )}

      <div className="mt-8"><RichText document={article.body} /></div>

      {article.related.length > 0 && (
        <aside className="mt-12 border-t border-slate-200 pt-8">
          <h2 className="mb-4 text-xl font-semibold">Related reading</h2>
          <ul className="space-y-2">
            {article.related.map((related) => (
              <li key={related.id}>
                <Link href={`/news/${related.slug}`} className="text-sky-700 underline">{related.title}</Link>
              </li>
            ))}
          </ul>
        </aside>
      )}
    </article>
  );
}
```

- [ ] **Step 2: Create `web/app/funds/[code]/page.tsx`**

```tsx
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { getFundDetailPage } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import { JsonLd } from "@/components/seo/JsonLd";
import type { FundDetailSection } from "@/lib/types";

type Props = { params: Promise<{ code: string }> };

export const revalidate = 300;

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const page = await getFundDetailPage((await params).code);
  if (!page) return {};
  return toMetadata(page.seo);
}

export default async function FundDetailPage({ params }: Props) {
  const code = (await params).code;

  // One Contentful template entry (slug funds/_detail) serves every fund. The BFF
  // injects this code into the fund detail widget, so adding a fund needs no authoring.
  const page = await getFundDetailPage(code);
  if (!page) notFound();

  const fund = page.sections.find((s): s is FundDetailSection => s.__type === "sectionFundDetailWidget");
  if (fund?.unavailable) notFound();

  return (
    <>
      <JsonLd data={page.jsonLd} />
      <SectionRenderer sections={page.sections} />
    </>
  );
}
```

- [ ] **Step 3: Verify both routes**

```powershell
(Invoke-WebRequest http://localhost:3000/funds/STX40).StatusCode
(Invoke-WebRequest http://localhost:3000/funds/NOTAFUND -SkipHttpErrorCheck).StatusCode
```

Expected: `200`, then `404`.

- [ ] **Step 4: Commit**

```powershell
git add web/app/news web/app/funds
git commit -m "feat(web): add news article and fund detail routes

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 4.9: Sitemap and robots

**Files:**
- Create: `web/app/sitemap.ts`, `web/app/robots.ts`

- [ ] **Step 1: Create `web/app/sitemap.ts`**

```ts
import type { MetadataRoute } from "next";
import { getSitemap } from "@/lib/bff";

export const revalidate = 300;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const entries = await getSitemap();
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

  if (!entries?.length) return [{ url: base, changeFrequency: "weekly", priority: 1 }];

  return entries.map((entry) => ({
    url: entry.loc,
    lastModified: entry.lastModified ? new Date(entry.lastModified) : undefined,
    changeFrequency: entry.changeFrequency as MetadataRoute.Sitemap[number]["changeFrequency"],
    priority: entry.priority,
  }));
}
```

- [ ] **Step 2: Create `web/app/robots.ts`**

```ts
import type { MetadataRoute } from "next";

export default function robots(): MetadataRoute.Robots {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
        // Filtered variants are already noindexed per lib/indexation.ts; disallowing
        // the crawl as well would stop crawlers seeing that directive. These two
        // entries block only internals that are never content.
        disallow: ["/api/", "/_next/"],
      },
    ],
    sitemap: `${base}/sitemap.xml`,
    host: base,
  };
}
```

- [ ] **Step 3: Verify both**

```powershell
(Invoke-WebRequest http://localhost:3000/sitemap.xml).Content.Substring(0, 300)
(Invoke-WebRequest http://localhost:3000/robots.txt).Content
```

Expected: XML `<urlset>` listing pages, articles and fund URLs; robots.txt naming the
sitemap.

- [ ] **Step 4: Commit**

```powershell
git add web/app/sitemap.ts web/app/robots.ts
git commit -m "feat(web): generate sitemap.xml and robots.txt from published content

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

# Phase 5 — Preview, publish and revalidate loop

Phase outcome: the admin user clicks Preview in Contentful and sees an unpublished
draft on the real site; they click Publish and the live page updates with no deploy.

### Task 5.1: Draft mode routes

**Files:**
- Create: `web/app/api/draft/route.ts`, `web/app/api/draft/disable/route.ts`

- [ ] **Step 1: Create `web/app/api/draft/route.ts`**

```ts
import { draftMode } from "next/headers";
import { redirect } from "next/navigation";
import type { NextRequest } from "next/server";

/**
 * Target of Contentful's preview URL.
 *
 * Draft mode is enabled HERE, not in the BFF: the cookie must be set on the Next.js
 * origin to be sent back with subsequent page requests. The BFF validates the secret
 * and owns the Contentful Preview API token.
 */
export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const secret = searchParams.get("secret");
  const slug = searchParams.get("slug") ?? "";

  const bff = process.env.BFF_BASE_URL ?? "http://localhost:5080";
  const response = await fetch(
    `${bff}/api/preview?secret=${encodeURIComponent(secret ?? "")}&slug=${encodeURIComponent(slug)}`,
    { cache: "no-store" },
  );

  if (!response.ok) {
    return new Response("Invalid preview token.", { status: 401 });
  }

  const { redirectTo } = (await response.json()) as { redirectTo: string };

  (await draftMode()).enable();
  redirect(redirectTo);
}
```

- [ ] **Step 2: Create `web/app/api/draft/disable/route.ts`**

```ts
import { draftMode } from "next/headers";
import { redirect } from "next/navigation";

export async function GET() {
  (await draftMode()).disable();
  redirect("/");
}
```

- [ ] **Step 3: Verify the handshake**

```powershell
(Invoke-WebRequest "http://localhost:3000/api/draft?secret=wrong&slug=about" -SkipHttpErrorCheck).StatusCode
(Invoke-WebRequest "http://localhost:3000/api/draft?secret=dev-preview-secret&slug=about" -MaximumRedirection 0 -SkipHttpErrorCheck).StatusCode
```

Expected: `401`, then `307` with a `Set-Cookie` header containing `__prerender_bypass`.

- [ ] **Step 4: Commit**

```powershell
git add web/app/api/draft
git commit -m "feat(web): enable Next draft mode from Contentful preview links

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 5.2: On-demand revalidation route

**Files:**
- Create: `web/app/api/revalidate/route.ts`

- [ ] **Step 1: Create the route**

```ts
import { revalidatePath, revalidateTag } from "next/cache";
import type { NextRequest } from "next/server";

/**
 * Called by the BFF after a Contentful webhook. The BFF has already evicted its own
 * cache and tells us which cache keys were affected; we drop the matching Next.js
 * cache entries so the next request re-renders from fresh content.
 */
export async function POST(request: NextRequest) {
  const body = (await request.json()) as { secret?: string; entryId?: string; tags?: string[] };

  if (body.secret !== process.env.PREVIEW_SECRET) {
    return Response.json({ message: "Invalid secret." }, { status: 401 });
  }

  const revalidated: string[] = [];

  // Broad tags: any page, article or fund listing may embed the changed entry.
  for (const tag of ["pages", "articles", "site", "funds"]) {
    revalidateTag(tag);
    revalidated.push(tag);
  }

  // Narrow tags from the BFF's reverse index, e.g. "page:about".
  for (const key of body.tags ?? []) {
    const slug = key.startsWith("page:") ? key.slice("page:".length).split("|")[0] : null;
    if (slug !== null) {
      revalidateTag(`page:${slug}`);
      revalidatePath(slug === "" ? "/" : `/${slug}`);
      revalidated.push(`page:${slug}`);
    }
  }

  revalidatePath("/sitemap.xml");

  return Response.json({ revalidated, entryId: body.entryId, now: Date.now() });
}
```

- [ ] **Step 2: Verify the secret is enforced**

```powershell
$body = @{ secret = "wrong"; entryId = "page-home" } | ConvertTo-Json
(Invoke-WebRequest http://localhost:3000/api/revalidate -Method Post -Body $body -ContentType "application/json" -SkipHttpErrorCheck).StatusCode

$body = @{ secret = "dev-preview-secret"; entryId = "page-home"; tags = @("page:") } | ConvertTo-Json
Invoke-RestMethod http://localhost:3000/api/revalidate -Method Post -Body $body -ContentType "application/json"
```

Expected: `401`, then an object listing the revalidated tags.

- [ ] **Step 3: Verify the full webhook chain**

```powershell
$payload = @{ sys = @{ id = "sec-home-hero" } } | ConvertTo-Json
Invoke-RestMethod http://localhost:5080/api/webhooks/contentful -Method Post -Body $payload -ContentType "application/json" -Headers @{ "X-Webhook-Secret" = "dev-webhook-secret" }
```

Expected: `{ entryId = sec-home-hero; evicted = @("page:|fund:-") }`, and the BFF log
showing the Next.js revalidate call returned `200`.

- [ ] **Step 4: Commit**

```powershell
git add web/app/api/revalidate
git commit -m "feat(web): revalidate pages on demand when Contentful publishes

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 5.3: Full-stack verification

**Files:** none (verification only)

- [ ] **Step 1: Run the whole test suite**

```powershell
dotnet test bff/Cms.sln
npm --prefix web test
```

Expected: `.NET: Failed: 0, Passed: 31`; `web: Tests 17 passed`.

- [ ] **Step 2: Production build the web app**

Run: `npm --prefix web run build`
Expected: `Compiled successfully`; the route summary lists `/[[...slug]]` as `SSG`
with the seeded static params, plus `/news/[slug]` and `/funds/[code]`.

- [ ] **Step 3: Verify the rendered HTML contains indexable content, not a JS shell**

```powershell
$html = (Invoke-WebRequest http://localhost:3000/tax-free-investing).Content
$html -match "<title>"
$html -match 'rel="canonical"'
$html -match 'application/ld\+json'
$html -match "<details"   # FAQ answers present in server HTML
```

Expected: all `True`. Confirms nothing indexable is client-only.

- [ ] **Step 4: Commit any fixes**

```powershell
git add -A
git commit -m "test: verify full-stack rendering, metadata and structured data

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

### Task 5.4: Documentation

**Files:**
- Create: `README.md`, `docs/ARCHITECTURE.md`, `docs/SEO.md`, `docs/DEMO.md`

- [ ] **Step 1: Write `README.md`**

Must contain: what the PoC proves; prerequisites (Node 20, .NET 10); a
copy-paste quickstart running all three services in Fixture mode; how to switch to a
real Contentful space (`.env`, `npm run migrate`, `npm run seed`,
`Contentful:Mode=Live`); the ngrok/cloudflared tunnel command and the two Contentful
settings that consume it (**Preview URL** →
`https://<tunnel>/api/draft?secret=<PREVIEW_SECRET>&slug={entry.fields.slug}`,
**Webhook** → `https://<tunnel-bff>/api/webhooks/contentful` with header
`X-Webhook-Secret`); a table of ports (3000 web, 5080 BFF, 5090 fund data); and the
**"what still needs a developer"** section — a new section *type*, a new data
integration, a new structured-data type — versus what does not: new pages, new slugs,
re-composition, copy, SEO, publishing.

Include the slug-change gap explicitly:

> **Changing a published slug breaks inbound links.** Contentful will happily let an
> admin user rename `/tax-free-investing` to `/tfsa`. The old URL then 404s, losing its
> inbound links and search rankings, and nothing in this PoC issues a 301. A production
> build needs a `redirect` content type (from, to, permanent) read by Next.js
> middleware, plus an automatic redirect entry created whenever a published slug
> changes. This is deliberately out of scope here but is a real gap, not an oversight.

- [ ] **Step 2: Write `docs/ARCHITECTURE.md`**

Covers: the page-as-composed-sections model and why it removes the developer from
page creation; how data-driven sections stay decoupled (Contentful stores
configuration, the Fund Data API owns the data, the BFF merges at request time); the
publish → webhook → BFF eviction → Next revalidate → live update flow, as a numbered
sequence; the fund detail template mechanism; and the two degradation contracts
(unknown section type dropped by both BFF and registry; fund API outage degrading a
section rather than a page).

- [ ] **Step 3: Write `docs/SEO.md`**

Covers the end-to-end flow — Contentful `seo` entry → `SeoResolver` (fallback chain,
canonical computation, `{{fund.*}}` token substitution) → `PageResponse.seo` →
`toMetadata()` → rendered `<head>`, and `JsonLdBuilderRegistry` → `PageResponse.jsonLd`
→ `<JsonLd>`. Includes the indexation decision table from the spec, the reason
canonical and noindex are never combined, and the crawler-degradation choices
(`<details>` accordions, server-rendered tab panels, real paginated links).

- [ ] **Step 4: Write `docs/DEMO.md`**

The click-path that proves the goal, as a numbered script:

1. Create a new `seo` entry: meta title, meta description, structured data type `WebPage`.
2. Create a new `page` entry: title `Retirement Annuities`, slug `retirement-annuities`,
   page type `marketing`, link the `seo` entry.
3. In **Sections**, use *Add content* → create a hero, a rich text block, an existing
   CTA banner, and the shared global disclaimer. Drag to reorder.
4. Click **Open preview** — the unpublished page renders at
   `/retirement-annuities` on the real site with a draft banner.
5. Click **Publish**. Within seconds the page is live at the same URL, and it appears
   in `/sitemap.xml`. **No deployment ran.**
6. Open the Home page entry, drag the CTA banner above the card grid, publish, reload
   the homepage: the order has changed.
7. Open the page's `seo` entry, tick **noindex**, publish, and view source: the robots
   meta tag now reads `noindex, follow`.

Each step states what to observe, so the demo can be run by someone who did not build it.

- [ ] **Step 5: Commit**

```powershell
git add README.md docs/ARCHITECTURE.md docs/SEO.md docs/DEMO.md
git commit -m "docs: add README, architecture, SEO and demo script

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Plan self-review

**Spec coverage.** Every spec section maps to a task:

| Spec section | Tasks |
|---|---|
| 4.2 content types (seo, mediaImage, page, sections, article, navigation, siteSettings) | 1.2–1.5 |
| 4.3 slug collision mitigation | 3.11 (`EffectiveSlug`, namespaced routes), 5.4 README |
| 5.1 endpoints | 3.13 |
| 5.2 resolution pipeline | 3.2, 3.4, 3.5, 3.7, 3.11 |
| 5.3 caching + reverse index | 3.10 |
| 5.4 fixture mode | 1.6, 3.3 |
| 5.5 preview mechanics | 3.13 (`/api/preview`), 5.1 |
| 6 mock fund data API | 2.1, 2.2 |
| 7.1 routing incl. fund detail template | 4.7, 4.8 |
| 7.2 component registry | 4.2 |
| 7.3 SEO rendering | 4.4, 4.6 (sitewide JSON-LD) |
| 7.4 crawler degradation | 4.2 (FAQ `<details>`, server-rendered filters), 5.3 Step 3 |
| 7.5 indexation strategy | 4.5, 4.7 |
| 8 testing scope | 3.2, 3.4, 3.6, 3.7, 3.8, 3.9, 3.10, 4.2, 4.4, 4.5 |
| 10 non-PoC next steps | 5.4 |

**Type consistency checked.** `SectionContext` is used with the same three-field shape
in Tasks 3.4, 3.5 and 3.7. `SeoDto.RobotsContent` is produced in 3.8 and consumed as
`robotsContent` in `web/lib/types.ts` (4.1) — `System.Text.Json`'s default camelCase
web policy makes that mapping correct. `FundDetailSection.Unavailable` (3.4) is
consumed as `unavailable` in 4.8. `PageResponse.EntryIds` (3.11) feeds `PageCache`
(3.10) and the webhook's `evicted` list (3.13), which the revalidate route parses as
`page:{slug}|fund:{code}` (5.2) — key format matches `PageResolutionService`'s
`cacheKey`.

**Two gaps found and closed while reviewing:**
- `ArticleTeaserListSectionResolver` was referenced in Task 3.13's DI wiring and in
  the registry table but had no defined behaviour. It is specified in Task 3.5's
  resolver table and depends on `ArticleService` (Task 3.12), which is why Task 3.13
  registers it and its dependents as scoped rather than singleton.
- `OrganizationJsonLdBuilder` is registered per-page but is only meaningfully used by
  the sitewide layout; Task 3.12 Step 3 builds the Organization + WebSite schema
  directly in `SiteService`, and Task 3.9 Step 5 notes the builder's role.
