import type { JsonLdObject } from "@/lib/types";

/**
 * Escapes the characters that could terminate the script element early. Content is
 * admin-authored, so this is defence in depth rather than a theoretical concern.
 */
function serialise(value: JsonLdObject): string {
  return JSON.stringify(value)
    .replace(/</g, "\\u003c")
    .replace(/>/g, "\\u003e")
    .replace(/&/g, "\\u0026");
}

export function JsonLd({ data }: { data: JsonLdObject[] }) {
  if (!data?.length) return null;

  return (
    <>
      {data.map((item, index) => (
        <script
          key={index}
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: serialise(item) }}
        />
      ))}
    </>
  );
}
