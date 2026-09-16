# Demo script

This is the click-path that proves the PoC's central claim: an admin user can create,
compose, preview and publish a page — and change an existing one — with no developer
involvement and no deployment. It assumes all three services are running (see the
quickstart in the root `README.md`) against a real Contentful space (see "Switching
to a real Contentful space" in the same file) with a tunnel exposing the web app and
the BFF, and that the Preview URL and webhook are configured as described there.

Each step below states what to observe, so it can be run by someone who did not build
this system.

## 1. Create a new `seo` entry

In Contentful, create a new **seo** entry:
- `internalName`: something identifiable, e.g. "Retirement Annuities SEO"
- `metaTitle`: e.g. "Retirement Annuities | [Site Name]"
- `metaDescription`: a sentence or two
- `structuredDataType`: `WebPage`

**Observe:** nothing renders yet — this entry isn't linked to a page. This step
exists to show that SEO is authored as its own reusable entry, not a field group
buried inside the page.

## 2. Create a new `page` entry

Create a new **page** entry:
- `title`: "Retirement Annuities"
- `slug`: `retirement-annuities`
- `pageType`: `marketing`
- `seo`: link the entry from step 1

**Observe:** the page entry does not yet have any sections, so it would currently
render as an empty page if published — but it isn't published yet.

## 3. Compose the page from sections

In the page entry's **Sections** field, use *Add content* to add, in order:
- a `sectionHero` (heading, subheading)
- a `sectionRichText` block (some body copy)
- an existing `sectionCtaBanner` entry already used elsewhere on the site
- the shared `sectionDisclaimer` entry used for compliance copy site-wide

Drag them to reorder — for example, move the CTA banner above the rich text block.

**Observe:** this is done entirely by picking and reordering existing entries; no new
content types or code were touched, and the shared CTA banner and disclaimer entries
are the exact same entries referenced by other pages (a change to either would affect
every page that uses it).

## 4. Preview the draft

Click **Open preview** on the page entry.

**Observe:** the browser opens `https://<your-web-tunnel>/retirement-annuities` (or
your production preview domain) and renders the page with all the sections just
composed, even though it has never been published. A draft-mode banner is visible on
the page (`web/components/DraftModeBanner`). This is the real site rendering real
draft content — not a Contentful-hosted preview iframe.

## 5. Publish

Click **Publish** on the page entry.

**Observe:** within a few seconds, reload
`https://<your-web-tunnel>/retirement-annuities` (or your production domain) without
draft mode — the page is now live at that URL, with the draft-mode banner gone.
Request `/sitemap.xml` and confirm `retirement-annuities` now appears in it. **No
deployment ran** — this is the webhook → BFF cache eviction → Next.js revalidate
pipeline described in `docs/ARCHITECTURE.md`, not a rebuild.

## 6. Reorder the homepage

Open the **Home** page entry (slug `""`). In its Sections field, drag the CTA banner
section above the card grid section. Publish.

Reload the homepage.

**Observe:** the CTA banner now appears above the card grid, matching the new order —
again live within seconds, with no deployment. This demonstrates that composition
changes to an *existing*, already-live page propagate the same way as publishing a
brand-new one.

## 7. Toggle noindex and verify

Open the `seo` entry linked to the page from step 2 (or any published page). Tick
**noindex**. Publish the `seo` entry.

Reload the page and view page source (or use your browser's "View Page Source", not
the rendered DOM, since this is a server-rendered meta tag).

**Observe:** the `<meta name="robots">` tag now reads `noindex, follow` (nofollow
stays untouched unless also ticked). This confirms the SEO pipeline described in
`docs/SEO.md` — `SeoResolver` on the BFF resolved the updated `seo` entry, and
`toMetadata` on the frontend turned `noIndex: true` into the robots meta tag — without
any code change or redeploy, and the update reached the live page through the same
webhook/revalidate pipeline as steps 5 and 6.
