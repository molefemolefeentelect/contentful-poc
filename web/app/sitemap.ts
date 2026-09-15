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
