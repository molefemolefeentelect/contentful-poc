import type { RichTextSection } from "@/lib/types";
import { RichText } from "@/components/RichText";

const widthClasses: Record<string, string> = {
  narrow: "max-w-2xl",
  default: "max-w-4xl",
  wide: "max-w-6xl",
};

export function RichTextBlock({ section }: { section: RichTextSection }) {
  const widthClass = widthClasses[section.width] ?? "max-w-4xl";

  return (
    <section className="mx-auto px-4 py-12">
      <div className={`mx-auto ${widthClass}`}>
        {section.heading && <h2 className="mb-4 text-2xl font-bold">{section.heading}</h2>}
        <RichText document={section.body} />
      </div>
    </section>
  );
}
