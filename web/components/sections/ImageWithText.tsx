import Image from "next/image";
import Link from "next/link";
import type { ImageWithTextSection } from "@/lib/types";
import { RichText } from "@/components/RichText";

export function ImageWithText({ section }: { section: ImageWithTextSection }) {
  const reverse = section.imagePosition === "right";

  return (
    <section className="mx-auto max-w-6xl px-4 py-12">
      <div className={`flex flex-col gap-8 md:flex-row ${reverse ? "md:flex-row-reverse" : ""}`}>
        {section.image && (
          <div className="md:w-1/2">
            <Image
              src={section.image.url}
              alt={section.image.altText}
              width={section.image.width ?? 600}
              height={section.image.height ?? 400}
              className="w-full rounded object-cover"
            />
          </div>
        )}
        <div className="md:w-1/2">
          {section.heading && <h2 className="mb-4 text-2xl font-bold">{section.heading}</h2>}
          <RichText document={section.body} />
          {section.cta && (
            <Link href={section.cta.url} className="mt-4 inline-block rounded bg-sky-600 px-5 py-3 font-semibold text-white hover:bg-sky-500">
              {section.cta.label}
            </Link>
          )}
        </div>
      </div>
    </section>
  );
}
