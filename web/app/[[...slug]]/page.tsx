import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { getPage, getSitemap } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { resolveIndexation, getSingleCategoryFilter } from "@/lib/indexation";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import { JsonLd } from "@/components/seo/JsonLd";
import type { FundListSection } from "@/lib/types";

type Props = {
  params: Promise<{ slug?: string[] }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const toSlug = (segments?: string[]) => (segments ?? []).join("/");

// The fund detail template is not a URL of its own; it is served via /funds/[code].
// Shared by generateMetadata and the page component so neither can drift from the other.
const isReservedSlug = (slug: string) => slug.startsWith("_") || slug.includes("/_");

// Statically generate every published URL the BFF knows about. Pages published later
// are served on demand and cached by ISR, so a new page needs no redeploy.
export async function generateStaticParams() {
  const base = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

  // getSitemap() throws (rather than resolving to null) for anything other than a 404
  // — including a BFF outage during the build. generateStaticParams has no
  // error-boundary equivalent either, so an uncaught throw here fails the ENTIRE
  // build, not just one page. Degrade to "no pages pre-rendered at build time" —
  // dynamicParams is already true, so every page still renders correctly on demand.
  let entries: Awaited<ReturnType<typeof getSitemap>>;
  try {
    entries = await getSitemap();
  } catch (error) {
    console.error("Failed to fetch sitemap entries from the BFF; skipping static generation for this build.", error);
    entries = null;
  }

  return (entries ?? [])
    .map((entry) => entry.loc.replace(base, "").replace(/^\//, ""))
    .filter((path) => !path.startsWith("news/") && !path.startsWith("funds/"))
    .map((path) => ({ slug: path === "" ? [] : path.split("/") }));
}

export const dynamicParams = true;
export const revalidate = 300;

export async function generateMetadata({ params, searchParams }: Props): Promise<Metadata> {
  const slug = toSlug((await params).slug);
  if (isReservedSlug(slug)) return {};

  const page = await getPage(slug);
  if (!page) return {};

  const query = await searchParams;
  const fundList = page.sections.find((s): s is FundListSection => s.__type === "sectionFundListWidget");

  // Filtered listing variants get an explicit indexation decision; ordinary pages do not.
  const options = fundList
    ? resolveIndexation(page.seo.canonicalUrl, query, fundList.categories.map((c) => c.id))
    : {};

  return toMetadata(page.seo, options);
}

export default async function CatchAllPage({ params, searchParams }: Props) {
  const slug = toSlug((await params).slug);

  if (isReservedSlug(slug)) notFound();

  const page = await getPage(slug);
  if (!page) notFound();

  const query = await searchParams;
  const categoryId = getSingleCategoryFilter(query);

  // Apply the category filter server-side so the filtered view is crawlable.
  const sections = categoryId
    ? page.sections.map((section) =>
        section.__type === "sectionFundListWidget"
          ? { ...section, funds: section.funds.filter((f) => f.categoryId === categoryId) }
          : section,
      )
    : page.sections;

  return (
    <>
      <JsonLd data={page.jsonLd} />
      <SectionRenderer sections={sections} />
    </>
  );
}
