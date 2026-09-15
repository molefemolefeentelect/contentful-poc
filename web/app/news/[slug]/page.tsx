import { notFound } from "next/navigation";
import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { getArticle } from "@/lib/bff";
import { toMetadata } from "@/lib/metadata";
import { RichText } from "@/components/RichText";
import { JsonLd } from "@/components/seo/JsonLd";

type Props = { params: Promise<{ slug: string }> };

export const revalidate = 300;

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const article = await getArticle((await params).slug);
  if (!article) return {};

  return {
    ...toMetadata(article.seo),
    openGraph: {
      ...toMetadata(article.seo).openGraph,
      type: "article",
      publishedTime: article.publishDate ?? undefined,
      modifiedTime: article.updatedDate ?? article.publishDate ?? undefined,
      authors: article.authorName ? [article.authorName] : undefined,
    },
  };
}

export default async function ArticlePage({ params }: Props) {
  const article = await getArticle((await params).slug);
  if (!article) notFound();

  return (
    <article className="mx-auto max-w-3xl px-4 py-12">
      <JsonLd data={article.jsonLd} />

      {article.categoryName && (
        <Link href={`/news?category=${article.categorySlug}`} className="text-sm font-semibold uppercase tracking-wide text-sky-700">
          {article.categoryName}
        </Link>
      )}
      <h1 className="mt-2 text-4xl font-bold leading-tight">{article.title}</h1>

      <p className="mt-3 text-sm text-slate-600">
        {article.authorName && <span>By {article.authorName}</span>}
        {article.publishDate && (
          <>
            {article.authorName && " · "}
            <time dateTime={article.publishDate}>
              {new Date(article.publishDate).toLocaleDateString("en-ZA", { year: "numeric", month: "long", day: "numeric" })}
            </time>
          </>
        )}
      </p>

      {article.featuredImage && (
        <Image
          src={article.featuredImage.url}
          alt={article.featuredImage.altText}
          width={article.featuredImage.width ?? 1200}
          height={article.featuredImage.height ?? 630}
          className="mt-8 w-full rounded"
          priority
        />
      )}

      <div className="mt-8"><RichText document={article.body} /></div>

      {article.related.length > 0 && (
        <aside className="mt-12 border-t border-slate-200 pt-8">
          <h2 className="mb-4 text-xl font-semibold">Related reading</h2>
          <ul className="space-y-2">
            {article.related.map((related) => (
              <li key={related.id}>
                <Link href={`/news/${related.slug}`} className="text-sky-700 underline">{related.title}</Link>
              </li>
            ))}
          </ul>
        </aside>
      )}
    </article>
  );
}
