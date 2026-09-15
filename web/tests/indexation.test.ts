import { describe, expect, it } from "vitest";
import { resolveIndexation, getSingleCategoryFilter } from "@/lib/indexation";

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

  it("noindexes a repeated categoryId of two known categories, since it requests a different content set than any single category", () => {
    expect(resolveIndexation(BASE, { categoryId: ["1", "2"] }, ["1", "2", "3"]))
      .toEqual({ canonicalOverride: undefined, forceNoIndex: true });
  });

  it("noindexes a repeated categoryId with a mix of known and unknown values the same way", () => {
    expect(resolveIndexation(BASE, { categoryId: ["1", "999"] }, ["1", "2", "3"]))
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

describe("getSingleCategoryFilter", () => {
  it("returns undefined for a repeated categoryId, matching resolveIndexation's treatment of it as not a single-category filter", () => {
    expect(getSingleCategoryFilter({ categoryId: ["1", "2"] })).toBeUndefined();
  });

  it("returns the value for a single categoryId", () => {
    expect(getSingleCategoryFilter({ categoryId: "1" })).toBe("1");
  });
});
