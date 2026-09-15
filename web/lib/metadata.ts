import type { Metadata } from "next";
import type { Seo } from "./types";

export type MetadataOptions = {
  /** Point the canonical at a different URL, e.g. a filtered listing to its base page. */
  canonicalOverride?: string;
  /** Force noindex for URL variants that should not be indexed at all. */
  forceNoIndex?: boolean;
};

export function toMetadata(seo: Seo, options: MetadataOptions = {}): Metadata {
  if (options.canonicalOverride && options.forceNoIndex) {
    console.error(
      "toMetadata: canonicalOverride and forceNoIndex must never be combined on the same URL " +
        "(search engines discard the canonical on a noindexed page unpredictably). " +
        "Check the caller's indexation logic.",
    );
  }

  // If a caller ever violates the invariant above, noindex wins: `index` below is derived
  // purely from noIndex/forceNoIndex and never from canonicalOverride, so a page that should
  // be hidden stays hidden even if it was also (wrongly) given a canonical override. Silently
  // failing to canonicalize is safe; silently indexing content the author wanted hidden is not.
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
