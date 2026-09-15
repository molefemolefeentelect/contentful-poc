import Image from "next/image";
import Link from "next/link";
import type { CardGridSection } from "@/lib/types";

const columnClasses: Record<number, string> = { 2: "md:grid-cols-2", 3: "md:grid-cols-3", 4: "md:grid-cols-4" };

export function CardGrid({ section }: { section: CardGridSection }) {
  const columnClass = columnClasses[section.columns] ?? "md:grid-cols-3";

  return (
    <section className="mx-auto max-w-6xl px-4 py-12">
      {section.heading && <h2 className="mb-2 text-2xl font-bold">{section.heading}</h2>}
      {section.intro && <p className="mb-8 max-w-2xl text-slate-600">{section.intro}</p>}
      <div className={`grid grid-cols-1 gap-6 ${columnClass}`}>
        {section.cards.map((card) => {
          const content = (
            <div className="h-full rounded border border-slate-200 p-6">
              {card.image && (
                <Image
                  src={card.image.url}
                  alt={card.image.altText}
                  width={card.image.width ?? 400}
                  height={card.image.height ?? 300}
                  className="mb-4 w-full rounded object-cover"
                />
              )}
              {card.icon && <div className="mb-2 text-2xl">{card.icon}</div>}
              <h3 className="text-lg font-semibold">{card.title}</h3>
              {card.body && <p className="mt-2 text-sm text-slate-600">{card.body}</p>}
            </div>
          );

          return (
            <div key={card.id}>
              {card.link ? <Link href={card.link.url}>{content}</Link> : content}
            </div>
          );
        })}
      </div>
    </section>
  );
}
