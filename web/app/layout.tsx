import type { Metadata } from "next";
import "./globals.css";
import { getSiteSettings } from "@/lib/bff";
import { Header } from "@/components/nav/Header";
import { Footer } from "@/components/nav/Footer";
import { JsonLd } from "@/components/seo/JsonLd";
import { DraftModeBanner } from "@/components/DraftModeBanner";
import type { SiteSettings } from "@/lib/types";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000"),
};

export default async function RootLayout({ children }: { children: React.ReactNode }) {
  let settings: SiteSettings | null = null;
  try {
    settings = await getSiteSettings();
  } catch (error) {
    console.error("Failed to fetch site settings; rendering with defaults.", error);
  }

  return (
    <html lang="en-ZA">
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        {/* Organization + WebSite, emitted once sitewide rather than per page. */}
        <JsonLd data={settings?.jsonLd ?? []} />
        <DraftModeBanner />
        <Header settings={settings} />
        <main>{children}</main>
        <Footer settings={settings} />
      </body>
    </html>
  );
}
