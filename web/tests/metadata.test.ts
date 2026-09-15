import { describe, expect, it, vi } from "vitest";
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
    expect(meta.twitter).toMatchObject({ card: "summary_large_image" });
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

  it("warns loudly (but does not throw) when canonicalOverride and forceNoIndex are combined, and noindex wins", () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    let meta: ReturnType<typeof toMetadata>;
    expect(() => {
      meta = toMetadata(seo, { canonicalOverride: "https://www.example.co.za/funds", forceNoIndex: true });
    }).not.toThrow();

    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy.mock.calls[0][0]).toContain("canonicalOverride and forceNoIndex must never be combined");

    // noindex wins: the page stays out of the index even though a (contradictory) canonical
    // override was also supplied.
    expect(meta!.robots).toMatchObject({ index: false });

    errorSpy.mockRestore();
  });
});
