import type { FaqAccordionSection } from "@/lib/types";
import { RichText } from "@/components/RichText";

export function FaqAccordion({ section }: { section: FaqAccordionSection }) {
  return (
    <section className="mx-auto max-w-3xl px-4 py-12">
      {section.heading && <h2 className="mb-6 text-2xl font-bold">{section.heading}</h2>}
      <div className="space-y-3">
        {section.items.map((item) => (
          <details key={item.id} className="rounded border border-slate-200 p-4">
            <summary className="cursor-pointer font-semibold">{item.question}</summary>
            <div className="mt-3">
              <RichText document={item.answer} />
            </div>
          </details>
        ))}
      </div>
    </section>
  );
}
