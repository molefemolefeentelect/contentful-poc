import Link from "next/link";
import type { CtaBannerSection } from "@/lib/types";

const variantClasses: Record<string, string> = {
  primary: "bg-sky-600 text-white",
  muted: "bg-slate-100 text-slate-900",
  accent: "bg-amber-500 text-slate-900",
};

export function CtaBanner({ section }: { section: CtaBannerSection }) {
  const variantClass = variantClasses[section.variant] ?? variantClasses.primary;

  return (
    <section className={`px-4 py-12 ${variantClass}`}>
      <div className="mx-auto flex max-w-4xl flex-col items-center gap-4 text-center">
        <h2 className="text-2xl font-bold">{section.heading}</h2>
        {section.body && <p className="max-w-2xl">{section.body}</p>}
        <Link href={section.cta.url} className="rounded bg-white px-5 py-3 font-semibold text-slate-900 hover:bg-slate-100">
          {section.cta.label}
        </Link>
      </div>
    </section>
  );
}
