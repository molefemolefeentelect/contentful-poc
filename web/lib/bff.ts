import { draftMode } from "next/headers";
import type {
  ArticleResponse, ArticleTeaser, PageResponse, SiteSettings, SitemapEntry,
} from "./types";

const BFF = process.env.BFF_BASE_URL ?? "http://localhost:5080";
const REVALIDATE_SECONDS = Number(process.env.REVALIDATE_SECONDS ?? 300);

async function isPreview(): Promise<boolean> {
  try {
    return (await draftMode()).isEnabled;
  } catch {
    // draftMode() throws outside a request scope, e.g. during sitemap generation.
    return false;
  }
}

async function get<T>(path: string, tags: string[]): Promise<T | null> {
  const preview = await isPreview();
  const separator = path.includes("?") ? "&" : "?";
  const url = `${BFF}${path}${preview ? `${separator}preview=true` : ""}`;

  const response = await fetch(url, {
    // Drafts must never be cached or shared between viewers.
    ...(preview
      ? { cache: "no-store" as const }
      : { next: { revalidate: REVALIDATE_SECONDS, tags } }),
  });

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`BFF ${response.status} for ${url}`);
  return (await response.json()) as T;
}

export function getPage(slug: string): Promise<PageResponse | null> {
  const path = slug === "" ? "/api/pages" : `/api/pages/${slug}`;
  return get<PageResponse>(path, ["pages", `page:${slug}`]);
}

export function getFundDetailPage(fundCode: string): Promise<PageResponse | null> {
  return get<PageResponse>(
    `/api/pages/funds/_detail?fundCode=${encodeURIComponent(fundCode)}`,
    ["pages", "funds", `fund:${fundCode}`],
  );
}

export function getArticle(slug: string): Promise<ArticleResponse | null> {
  return get<ArticleResponse>(`/api/articles/${slug}`, ["articles", `article:${slug}`]);
}

export function getArticles(category?: string, page = 1): Promise<{ items: ArticleTeaser[]; total: number } | null> {
  const query = new URLSearchParams({ page: String(page) });
  if (category) query.set("category", category);
  return get<{ items: ArticleTeaser[]; total: number }>(`/api/articles?${query}`, ["articles"]);
}

export function getSiteSettings(): Promise<SiteSettings | null> {
  return get<SiteSettings>("/api/site-settings", ["site"]);
}

export function getSitemap(): Promise<SitemapEntry[] | null> {
  return get<SitemapEntry[]>("/api/sitemap", ["pages", "articles", "funds"]);
}
