import Image from "next/image";
import Link from "next/link";
import type { HeroSection } from "@/lib/types";

export function Hero({ section }: { section: HeroSection }) {
  return (
    <section className="relative isolate overflow-hidden bg-slate-900 text-white">
      {section.backgroundImage && (
        <Image
          src={section.backgroundImage.url}
          alt={section.backgroundImage.altText}
          fill
          priority
          sizes="100vw"
          className="absolute inset-0 -z-10 object-cover opacity-40"
        />
      )}
      <div className="mx-auto max-w-5xl px-4 py-20">
        {section.eyebrow && <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-sky-300">{section.eyebrow}</p>}
        <h1 className="text-4xl font-bold leading-tight sm:text-5xl">{section.heading}</h1>
        {section.subheading && <p className="mt-4 max-w-2xl text-lg text-slate-200">{section.subheading}</p>}
        <div className="mt-8 flex flex-wrap gap-3">
          {section.primaryCta && (
            <Link href={section.primaryCta.url} className="rounded bg-sky-500 px-5 py-3 font-semibold text-white hover:bg-sky-400">
              {section.primaryCta.label}
            </Link>
          )}
          {section.secondaryCta && (
            <Link href={section.secondaryCta.url} className="rounded border border-white/40 px-5 py-3 font-semibold hover:bg-white/10">
              {section.secondaryCta.label}
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}
