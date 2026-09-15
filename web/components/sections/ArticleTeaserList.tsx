import Image from "next/image";
import Link from "next/link";
import type { ArticleTeaserListSection } from "@/lib/types";

export function ArticleTeaserList({ section }: { section: ArticleTeaserListSection }) {
  return (
    <section className="mx-auto max-w-6xl px-4 py-12">
      {section.heading && <h2 className="mb-6 text-2xl font-bold">{section.heading}</h2>}
      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        {section.articles.map((article) => (
          <Link key={article.id} href={`/news/${article.slug}`} className="block rounded border border-slate-200 p-4 hover:border-slate-400">
            {article.image && (
              <Image
                src={article.image.url}
                alt={article.image.altText}
                width={article.image.width ?? 400}
                height={article.image.height ?? 300}
                className="mb-3 w-full rounded object-cover"
              />
            )}
            {article.categoryName && <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-sky-600">{article.categoryName}</p>}
            <h3 className="text-lg font-semibold">{article.title}</h3>
            {article.excerpt && <p className="mt-2 text-sm text-slate-600">{article.excerpt}</p>}
            <div className="mt-3 text-xs text-slate-500">
              {article.publishDate && (
                <time dateTime={article.publishDate}>{new Date(article.publishDate).toLocaleDateString()}</time>
              )}
              {article.authorName && <span>{article.publishDate ? " · " : ""}{article.authorName}</span>}
            </div>
          </Link>
        ))}
      </div>
    </section>
  );
}
