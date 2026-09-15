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
 *
 * Caller contract: `knownCategoryIds` must be the set of categories THIS SPECIFIC
 * widget instance can actually render — e.g. `section.categories.map(c => c.id)`
 * from the resolved `FundListSection` on the current page — not a global category
 * list fetched independently. If a widget is itself pinned to one category
 * server-side, its `knownCategoryIds` should reflect that narrower set, so a
 * request for a globally-valid but locally-unrenderable category is correctly
 * noindexed rather than wrongly canonicalized to a page that would render no
 * matching content.
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

    if (Array.isArray(value)) {
      // A repeated categoryId (?categoryId=1&categoryId=2) requests a different
      // content set than any single category, so it's not a near-duplicate of the
      // base listing — treat it like any other multi-param combination.
      return { canonicalOverride: undefined, forceNoIndex: true };
    }

    return knownCategoryIds.includes(value ?? "")
      ? { canonicalOverride: baseUrl, forceNoIndex: false }
      : { canonicalOverride: undefined, forceNoIndex: true };
  }

  return { canonicalOverride: undefined, forceNoIndex: true };
}
