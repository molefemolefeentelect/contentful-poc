import { revalidatePath, revalidateTag } from "next/cache";
import type { NextRequest } from "next/server";

/**
 * Called by the BFF after a Contentful webhook. The BFF has already evicted its own
 * cache and tells us which cache keys were affected; we drop the matching Next.js
 * cache entries so the next request re-renders from fresh content.
 *
 * The BFF's cache keys (see bff/src/Cms.Bff/Caching/PageCache.cs and the *Service
 * classes that build them) look like:
 *   "page:{slug}|fund:{fundCode|-}"   e.g. "page:about|fund:-", "page:funds/_detail|fund:ABC"
 *   "article:{slug}"
 *   "articles:{category|-}:{page}:{pageSize}"
 *   "site-settings:full" / "site-settings:default-seo" / "site-settings:default-seo:article"
 *
 * These are NOT the same strings as the Next.js cache tags used in web/lib/bff.ts, so we
 * can't just call revalidateTag(key) directly. In particular, a fund detail page is cached
 * by the BFF under the template's own slug ("page:funds/_detail|fund:ABC") but Next.js
 * fetches it tagged as ["pages", "funds", `fund:${fundCode}`] (see getFundDetailPage in
 * web/lib/bff.ts) and renders it at /funds/{code}, not /funds/_detail. So a "page:" key
 * carrying a real fund code must be translated into a `fund:{code}` tag/path, not a
 * `page:funds/_detail` one.
 */

type RevalidateBody = {
  secret?: unknown;
  entryId?: unknown;
  tags?: unknown;
};

function parsePageCacheKey(key: string): { slug: string; fundCode: string | null } | null {
  if (!key.startsWith("page:")) return null;

  const rest = key.slice("page:".length); // e.g. "about|fund:-" or "funds/_detail|fund:ABC"
  const [slug, fundPart] = rest.split("|fund:");
  const fundCode = fundPart && fundPart !== "-" ? fundPart : null;

  return { slug, fundCode };
}

export async function POST(request: NextRequest) {
  let body: RevalidateBody;
  try {
    body = (await request.json()) as RevalidateBody;
  } catch {
    return Response.json({ message: "Malformed JSON body." }, { status: 400 });
  }

  if (body.secret !== process.env.PREVIEW_SECRET) {
    return Response.json({ message: "Invalid secret." }, { status: 401 });
  }

  const tags = Array.isArray(body.tags) ? body.tags.filter((t): t is string => typeof t === "string") : [];
  const entryId = typeof body.entryId === "string" ? body.entryId : undefined;

  const revalidated: string[] = [];

  // Broad tags: any page, article or fund listing may embed the changed entry.
  // These names must match the `next: { tags: [...] }` values fetches use in web/lib/bff.ts.
  for (const tag of ["pages", "articles", "site", "funds"]) {
    revalidateTag(tag);
    revalidated.push(tag);
  }

  // Narrow tags from the BFF's reverse index, e.g. "page:about" or "page:funds/_detail|fund:ABC".
  for (const key of tags) {
    const parsed = parsePageCacheKey(key);
    if (parsed === null) continue;

    if (parsed.fundCode !== null) {
      // A fund detail page: narrow by fund code, matching getFundDetailPage's tag/URL.
      revalidateTag(`fund:${parsed.fundCode}`);
      revalidatePath(`/funds/${parsed.fundCode}`);
      revalidated.push(`fund:${parsed.fundCode}`);
    } else {
      revalidateTag(`page:${parsed.slug}`);
      revalidatePath(parsed.slug === "" ? "/" : `/${parsed.slug}`);
      revalidated.push(`page:${parsed.slug}`);
    }
  }

  revalidatePath("/sitemap.xml");

  return Response.json({ revalidated, entryId, now: Date.now() });
}
