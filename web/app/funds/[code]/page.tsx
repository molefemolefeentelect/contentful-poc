import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { getFundDetailPage } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { SectionRenderer } from "@/components/sections/SectionRenderer";
import { JsonLd } from "@/components/seo/JsonLd";
import type { FundDetailSection } from "@/lib/types";

type Props = { params: Promise<{ code: string }> };

export const revalidate = 300;

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const page = await getFundDetailPage((await params).code);
  if (!page) return {};
  return toMetadata(page.seo);
}

export default async function FundDetailPage({ params }: Props) {
  const code = (await params).code;

  // One Contentful template entry (slug funds/_detail) serves every fund. The BFF
  // injects this code into the fund detail widget, so adding a fund needs no authoring.
  const page = await getFundDetailPage(code);
  if (!page) notFound();

  const fund = page.sections.find((s): s is FundDetailSection => s.__type === "sectionFundDetailWidget");
  if (!fund || fund.unavailable) notFound();

  return (
    <>
      <JsonLd data={page.jsonLd} />
      <SectionRenderer sections={page.sections} />
    </>
  );
}
