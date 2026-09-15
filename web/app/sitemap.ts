import type { MetadataRoute } from "next";
import { getSitemap } from "@/lib/bff";

export const revalidate = 300;

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

  // getSitemap() throws (rather than resolving to null) for anything other than a 404
  // — including a BFF outage. sitemap.ts is a metadata route with no error-boundary
  // equivalent, so an uncaught throw here fails the whole build/ISR regeneration, not
  // just this one route. Degrade to a minimal sitemap instead.
  let entries: Awaited<ReturnType<typeof getSitemap>>;
  try {
    entries = await getSitemap();
  } catch (error) {
    console.error("Failed to fetch sitemap entries from the BFF; falling back to a minimal sitemap.", error);
    entries = null;
  }

  if (!entries?.length) return [{ url: base, changeFrequency: "weekly", priority: 1 }];

  return entries.map((entry) => ({
    url: entry.loc,
    lastModified: entry.lastModified ? new Date(entry.lastModified) : undefined,
    changeFrequency: entry.changeFrequency as MetadataRoute.Sitemap[number]["changeFrequency"],
    priority: entry.priority,
  }));
}
