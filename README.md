# Composable CMS PoC

## What this proves

A marketing site where an admin user — not a developer — creates a page, composes it
from reusable content blocks ("sections"), previews it as a draft on the real site,
publishes it, and sees it live at its URL within seconds, with no deployment. Pages
that need live data (fund prices, performance) are built the same way: the admin
authors configuration in Contentful, and a backend-for-frontend (BFF) merges in data
from a separate Fund Data API at request time.

Concretely, this PoC demonstrates:

- **Page composition without code.** A `page` entry is a title, an SEO entry, and an
  ordered list of section entries (hero, rich text, card grid, CTA banner, FAQ
  accordion, disclaimer, etc.). Reordering, adding or removing sections requires no
  code change and no deploy.
- **Data-driven pages that stay decoupled.** Fund list and fund detail sections carry
  *configuration only* (which category, which fund code, which fields to show).  No
  fund name or price is ever stored in Contentful — it comes from a separate Fund Data
  API at request time, merged in by the BFF.
- **A working publish → live pipeline.** Publishing in Contentful fires a webhook to
  the BFF, which evicts exactly the cached pages that embed the changed entry and
  tells Next.js to revalidate the same URLs. The page is live at its existing URL
  within seconds — no rebuild, no redeploy.
- **SEO and structured data driven entirely from Contentful.** Meta tags, canonical
  URLs, robots directives and JSON-LD are all resolved by the BFF from a `seo` entry
  (with a site-wide fallback) and rendered by Next.js. No page hand-codes its own
  metadata or schema.
- **Deliberate degradation, not crashes.** An unrecognised section type is dropped and
  logged, on both the BFF and the frontend, rather than failing the page. A Fund Data
  API outage degrades the one section that needed it, not the whole page. A BFF
  outage degrades the sitemap and static generation rather than breaking the Next.js
  build.
- **A real, deliberate gap.** See "Changing a published slug" below.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for how the pieces fit together,
[`docs/SEO.md`](docs/SEO.md) for the SEO/structured-data pipeline, and
[`docs/DEMO.md`](docs/DEMO.md) for a click-by-click script that exercises all of this.

## Prerequisites

- Node.js 20 (repo was built and tested against Node 20.20.2)
- .NET SDK 10 (repo was built and tested against 10.0.401)
- No Contentful account needed to run the quickstart below — the BFF ships a fixture
  mode that reads seeded JSON in genuine Contentful Delivery API response shape.

## Quickstart (Fixture mode, no Contentful account)

This runs all three services against the checked-in fixture data at
`contentful/seed/fixtures/entries.json`. `Contentful:Mode` defaults to `Fixture` in
`bff/src/Cms.Bff/appsettings.json`, so no configuration is required.

Open three terminals from the repo root:

```powershell
# Terminal 1 — Fund Data API (port 5090)
cd bff
dotnet run --project src/Cms.FundData.Api
```

```powershell
# Terminal 2 — BFF (port 5080)
cd bff
dotnet run --project src/Cms.Bff
```

```powershell
# Terminal 3 — Next.js frontend (port 3000)
cd web
cp .env.local.example .env.local   # first time only
npm install                        # first time only
npm run dev
```

Open `http://localhost:3000`. Every page, section and fund on the site is coming from
the fixture data and the mock Fund Data API — nothing here talks to a real Contentful
space yet.

## Switching to a real Contentful space

1. Create a Contentful space and grab its Space ID, a Content Management API token,
   and Delivery/Preview API tokens.

2. Configure the migration/seed tooling:

   ```powershell
   cd contentful
   cp .env.example .env
   ```

   Fill in `.env`:

   ```
   CONTENTFUL_SPACE_ID=<your space id>
   CONTENTFUL_ENVIRONMENT=master
   CONTENTFUL_MANAGEMENT_TOKEN=<content management API token>
   CONTENTFUL_DELIVERY_TOKEN=<delivery API token>
   CONTENTFUL_PREVIEW_TOKEN=<preview API token>
   ```

3. Apply the content model and seed data:

   ```powershell
   npm install    # first time only
   npm run migrate
   npm run seed
   ```

   `npm run migrate` runs every `.cjs` file in `contentful/migrations/` in order,
   creating the `seo`, `mediaImage`, `page`, the ten section types, `article`,
   `navigation` and related content types. `npm run seed` populates them from
   `contentful/seed/`.

4. Point the BFF at the live space instead of the fixture. Do **not** put real tokens
   in `appsettings.json` — use user-secrets locally:

   ```powershell
   cd bff/src/Cms.Bff
   dotnet user-secrets set "Contentful:Mode" "Live"
   dotnet user-secrets set "Contentful:SpaceId" "<your space id>"
   dotnet user-secrets set "Contentful:Environment" "master"
   dotnet user-secrets set "Contentful:DeliveryToken" "<delivery API token>"
   dotnet user-secrets set "Contentful:PreviewToken" "<preview API token>"
   ```

   `GET /health` on the BFF reports `{"mode":"Live"}` once this is picked up.

5. Restart the BFF. The site now renders from the real space.

## Preview and webhook tunnels

Contentful's "Open preview" button and its webhooks need to reach your machine from
the internet. Use a tunnel:

```powershell
ngrok http 3000   # tunnels the Next.js app; note the https URL it prints
ngrok http 5080   # tunnels the BFF; note this https URL too
```

(`cloudflared tunnel --url http://localhost:3000` works the same way.)

Two settings in Contentful consume these URLs:

- **Preview URL** (on the `page`/`article` content type's Preview settings):

  ```
  https://<your-web-tunnel>/api/draft?secret=<PREVIEW_SECRET>&slug={entry.fields.slug}
  ```

- **Webhook** (Settings → Webhooks → create webhook, triggered on publish/unpublish):

  - URL: `https://<your-bff-tunnel>/api/webhooks/contentful`
  - Header: `X-Webhook-Secret: <WebhookSecret>`

`PREVIEW_SECRET` (web) must match `Integration:PreviewSecret` (BFF); the webhook
header must match `Integration:WebhookSecret` (BFF). For local dev both default to
`dev-preview-secret` and `dev-webhook-secret` respectively — see
`bff/src/Cms.Bff/appsettings.json` and `web/.env.local.example`. Change these before
exposing either tunnel beyond your own testing.

## Ports

| Service | Port | Purpose |
|---|---|---|
| Next.js web app | 3000 | Renders pages, handles draft mode, revalidation, sitemap/robots |
| .NET BFF (`Cms.Bff`) | 5080 | Resolves Contentful content into page/article/SEO/JSON-LD responses; handles webhooks and preview token validation |
| Fund Data API (`Cms.FundData.Api`) | 5090 | Mock market-data service — fund list, fund detail, price history, categories |

## What still needs a developer, versus what doesn't

**Does not need a developer** (an admin user does this entirely in Contentful):

- New pages, at any slug
- New slugs for existing pages (subject to the gap below)
- Re-composing a page: adding, removing, reordering existing section types
- Copy changes, image swaps, disclaimer text
- SEO: titles, descriptions, canonical overrides, noindex/nofollow, structured-data
  type
- Publishing, unpublishing, and previewing drafts

**Needs a developer:**

- A genuinely new section *type* (a new visual block Contentful has never modelled) —
  requires a new Contentful content type/migration, a BFF resolver, a BFF DTO with a
  `__type` discriminator, and a frontend `registry.ts` entry.
- A new data integration (a data source beyond the Fund Data API) — requires a new BFF
  client and resolver.
- A new structured-data type — requires a new `IJsonLdBuilder` on the BFF.

## Changing a published slug breaks inbound links

Contentful will happily let an admin user rename `/tax-free-investing` to `/tfsa`. The
old URL then 404s, losing its inbound links and search rankings, and nothing in this
PoC issues a 301. A production build needs a `redirect` content type (from, to,
permanent) read by Next.js middleware, plus an automatic redirect entry created
whenever a published slug changes. This is deliberately out of scope here but is a
real gap, not an oversight.

## Known model gap: cross-type slug collisions

Contentful's slug-uniqueness validation is per content type, so a `page` and an
`article` could theoretically share a slug. This is mitigated by namespacing the URL
space (pages at root, articles under `/news/{slug}`, funds under `/funds/{code}`) and
by the BFF resolving in fixed precedence, logging a warning on ambiguity — not solved
outright. See `docs/ARCHITECTURE.md`.

## Testing

```powershell
cd bff
dotnet test tests/Cms.Bff.Tests/Cms.Bff.Tests.csproj
```

```powershell
cd web
npm test
```

As of the last full verification pass in this session: 56 BFF tests and 22 web tests,
all passing.
