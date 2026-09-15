export type Image = { url: string; altText: string; width?: number; height?: number; caption?: string };
export type Link = { label: string; url: string };

export type Seo = {
  metaTitle: string;
  metaDescription: string;
  ogTitle: string;
  ogDescription: string;
  ogImage?: Image | null;
  canonicalUrl: string;
  noIndex: boolean;
  noFollow: boolean;
  structuredDataType: string;
  /** Author-authored raw JSON merged over generated JSON-LD server-side; rarely needed client-side. */
  structuredDataOverrides?: Record<string, unknown> | null;
  robotsContent: string;
};

export type Breadcrumb = { name: string; url: string };

type Base<T extends string> = { __type: T; id: string };

export type HeroSection = Base<"sectionHero"> & {
  eyebrow?: string | null;
  heading: string;
  subheading?: string | null;
  backgroundImage?: Image | null;
  primaryCta?: Link | null;
  secondaryCta?: Link | null;
  variant: string;
};

export type RichTextSection = Base<"sectionRichText"> & {
  heading?: string | null;
  body?: unknown;
  width: string;
};

export type Card = { id: string; title: string; body?: string | null; image?: Image | null; link?: Link | null; icon?: string | null };
export type CardGridSection = Base<"sectionCardGrid"> & { heading?: string | null; intro?: string | null; cards: Card[]; columns: number };

export type CtaBannerSection = Base<"sectionCtaBanner"> & { heading: string; body?: string | null; cta: Link; variant: string };

export type ArticleTeaser = {
  id: string; title: string; slug: string; excerpt?: string | null;
  image?: Image | null; categoryName?: string | null; publishDate?: string | null; authorName?: string | null;
};
export type ArticleTeaserListSection = Base<"sectionArticleTeaserList"> & { heading?: string | null; articles: ArticleTeaser[] };

export type FundSummary = {
  code: string; name: string; shortDescription: string; categoryId: string;
  nav: number; dayChangePercent: number; ter: number; oneYearReturn: number;
};
export type FundCategory = { id: string; name: string; slug: string };
export type FundListSection = Base<"sectionFundListWidget"> & {
  heading?: string | null; intro?: string | null; categoryId?: string | null;
  showFilters: boolean; displayVariant: string; categories: FundCategory[]; funds: FundSummary[];
};

export type PricePoint = { date: string; nav: number };
export type FundPerformance = { oneYear: number; threeYear: number; fiveYear: number; sinceInception: number };
export type FundDetailSection = Base<"sectionFundDetailWidget"> & {
  code?: string | null; name?: string | null; isin?: string | null; shortDescription?: string | null;
  nav?: number | null; dayChangePercent?: number | null; ter?: number | null;
  inceptionDate?: string | null; factsheetUrl?: string | null;
  performance?: FundPerformance | null; prices: PricePoint[]; unavailable: boolean;
};

export type FaqItem = { id: string; question: string; answer?: unknown; plainTextAnswer: string };
export type FaqAccordionSection = Base<"sectionFaqAccordion"> & { heading?: string | null; items: FaqItem[]; emitFaqSchema: boolean };

export type ImageWithTextSection = Base<"sectionImageWithText"> & {
  heading?: string | null; body?: unknown; image?: Image | null; imagePosition: string; cta?: Link | null;
};

export type DisclaimerSection = Base<"sectionDisclaimer"> & {
  label?: string | null; body?: unknown; severity: string; collapsible: boolean;
};

export type Section =
  | HeroSection | RichTextSection | CardGridSection | CtaBannerSection
  | ArticleTeaserListSection | FundListSection | FundDetailSection
  | FaqAccordionSection | ImageWithTextSection | DisclaimerSection;

export type JsonLdObject = Record<string, unknown>;

export type PageResponse = {
  id: string; slug: string; title: string; pageType: "marketing" | "dataDriven";
  seo: Seo; jsonLd: JsonLdObject[]; breadcrumbs: Breadcrumb[]; sections: Section[];
  updatedAt?: string | null; entryIds: string[];
};

export type ArticleResponse = {
  id: string; slug: string; title: string; excerpt?: string | null; body?: unknown;
  featuredImage?: Image | null; authorName?: string | null; authorJobTitle?: string | null;
  categoryName?: string | null; categorySlug?: string | null; tags: string[];
  publishDate?: string | null; updatedDate?: string | null;
  seo: Seo; jsonLd: JsonLdObject[]; related: ArticleTeaser[]; entryIds: string[];
};

export type NavigationItem = { label: string; url: string; external: boolean; children: NavigationItem[] };
export type Navigation = { key: string; items: NavigationItem[] };
export type SiteSettings = {
  siteName: string; logo?: Image | null; disclaimerText?: unknown;
  socialLinks: { label: string; url: string }[]; navigations: Navigation[]; jsonLd: JsonLdObject[];
};

export type SitemapEntry = { loc: string; lastModified?: string | null; changeFrequency: string; priority: number };
