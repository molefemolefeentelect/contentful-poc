# CDA-shaped fixtures

`entries.json` is hand-authored demo content shaped exactly like a real
Contentful Delivery API (CDA) response: a top-level object with `sys`, `total`,
`skip`, `limit`, `items` (the directly-queried entries) and an
`includes.Entry` / `includes.Asset` block (every entry or asset those items
link to, transitively).

Every reference — `page.seo`, `page.sections`, `sectionCardGrid.cards`,
`mediaImage.image`, etc. — is an **unresolved Link node**:

```json
{ "sys": { "id": "sec-home-hero", "type": "Link", "linkType": "Entry" } }
```

or `"linkType": "Asset"` for asset references. Fields are **not**
locale-wrapped (no `{"en-US": "..."}`) — just the flat value — matching how
Contentful returns data when you don't pass `locale=*`.

This exists so a later `FixtureContentfulClient` in the BFF can serve the
entire stack — no real Contentful space or API token required — while
exercising the *same* link-resolution code path the BFF uses against a real
CDA response. The client is expected to find any entry by scanning `items` +
`includes.Entry` filtered by content type (and `fields.slug` where relevant),
so every `page`, `article`, `navigation`, and the `siteSettings` entry lives
in top-level `items` (these are the entries the BFF queries directly by
content type / slug), while everything only ever reached via a Link — section
blocks, `seo`, `card`, `faqItem`, `mediaImage`, `navigationItem`, `category`,
`author` — lives in `includes.Entry`. Assets referenced by any `mediaImage`
live in `includes.Asset`.

## Content types covered

Schemas for all of these are defined in the migrations under
`contentful/migrations/`:

- `001-foundation.cjs` — `mediaImage`, `seo`, `category`, `author`,
  `siteSettings`
- `002-sections.cjs` — `card`, `faqItem`, and the ten `section*` block types
  (`sectionHero`, `sectionRichText`, `sectionCardGrid`, `sectionCtaBanner`,
  `sectionArticleTeaserList`, `sectionFundListWidget`,
  `sectionFundDetailWidget`, `sectionFaqAccordion`, `sectionImageWithText`,
  `sectionDisclaimer`)
- `003-composition.cjs` — `page`, `article`, `navigationItem`, `navigation`
- `004-validations.cjs` — the section-type whitelist enforced on
  `page.sections` in real Contentful (informational only — this fixture file
  has no validation layer, so it can and deliberately does include an entry
  outside that whitelist; see below)

Field names in every fixture entry match those migrations exactly.

## Demo content

Seven pages (`page-home` "", `page-about` `about`, `page-how-to-invest`
`how-to-invest`, `page-tax-free` `tax-free-investing`, `page-funds` `funds`,
`page-fund-detail` `funds/_detail`, `page-news` `news`), four articles, two
navigation menus (`nav-header`, `nav-footer`) and one `settings-main`
`siteSettings` entry, loosely themed on a fictional South African
index-fund manager. `page-fund-detail`'s `sectionFundDetailWidget` entry
(`sec-fund-detail-widget`) intentionally omits `fundCode` — that field is
meant to be filled in from the route (`/funds/:code`) at request time, not
from content.

## The deliberately-unknown section (`sec-unknown`)

`page-about` references a section entry, `sec-unknown`, whose content type is
`sectionFutureThing` — a content type that does not exist in any migration
and never will. It's included on purpose: the BFF's link-resolution /
section-rendering code is expected to skip section blocks it doesn't
recognise rather than fail the whole page render. Unit tests can (and should)
cover that contract synthetically, but this fixture also exercises it with a
real, otherwise-normal page in the demo dataset — if a future change makes
"unknown section type" fatal instead of degrading gracefully, rendering
`/about` against this fixture set should surface it.

## Regenerating

There's no live generator checked in — the file was authored directly. If you
need to add content, edit `entries.json` by hand following the existing
entries as a template, keeping to the Link-node / non-locale-wrapped shape
described above, then re-verify with:

```powershell
node -e "const d=require('./contentful/seed/fixtures/entries.json'); const ids=new Set([...d.items,...d.includes.Entry].map(e=>e.sys.id)); const missing=[]; JSON.stringify(d,(k,v)=>{if(v&&v.sys&&v.sys.linkType==='Entry'&&!ids.has(v.sys.id))missing.push(v.sys.id);return v}); console.log(missing.length?('DANGLING: '+missing.join(',')):'all entry links resolve')"
```
