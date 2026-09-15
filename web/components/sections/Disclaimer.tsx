import type { DisclaimerSection } from "@/lib/types";
import { RichText } from "@/components/RichText";

const severityClasses: Record<string, string> = {
  info: "bg-slate-50 border-slate-300 text-slate-700",
  warning: "bg-amber-50 border-amber-400 text-amber-900",
};

export function Disclaimer({ section }: { section: DisclaimerSection }) {
  const colorClass = severityClasses[section.severity] ?? severityClasses.info;
  const summaryLabel = section.label ?? "Important information";

  const content = (
    <>
      {section.collapsible ? null : section.label && <p className="mb-2 font-semibold">{section.label}</p>}
      <div className="text-sm">
        <RichText document={section.body} />
      </div>
    </>
  );

  return (
    <section className="mx-auto max-w-4xl px-4 py-8">
      <div className={`rounded border p-4 ${colorClass}`}>
        {section.collapsible ? (
          <details>
            <summary className="cursor-pointer font-semibold">{summaryLabel}</summary>
            <div className="mt-3 text-sm">
              <RichText document={section.body} />
            </div>
          </details>
        ) : (
          content
        )}
      </div>
    </section>
  );
}
