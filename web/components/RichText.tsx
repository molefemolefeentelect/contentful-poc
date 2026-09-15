import { documentToReactComponents, type Options } from "@contentful/rich-text-react-renderer";
import { BLOCKS, INLINES, type Document } from "@contentful/rich-text-types";
import Image from "next/image";
import Link from "next/link";
import type { ReactNode } from "react";

const options: Options = {
  renderNode: {
    [BLOCKS.PARAGRAPH]: (_node, children: ReactNode) => <p className="mb-4 leading-relaxed">{children}</p>,
    [BLOCKS.HEADING_2]: (_node, children: ReactNode) => <h2 className="mb-3 mt-8 text-2xl font-bold">{children}</h2>,
    [BLOCKS.HEADING_3]: (_node, children: ReactNode) => <h3 className="mb-2 mt-6 text-xl font-semibold">{children}</h3>,
    [BLOCKS.UL_LIST]: (_node, children: ReactNode) => <ul className="mb-4 list-disc space-y-1 pl-6">{children}</ul>,
    [BLOCKS.OL_LIST]: (_node, children: ReactNode) => <ol className="mb-4 list-decimal space-y-1 pl-6">{children}</ol>,
    // NOTE: the BFF's EntryLinkResolver.ConvertValue does not walk into rich-text document
    // trees when resolving Links — a RichText field is passed through via value.Clone()
    // untouched (see EntryLinkResolver.cs), so an embedded-asset node's node.data.target
    // still arrives here as the raw, unresolved Contentful shape ({ sys: { id, type: "Link",
    // linkType: "Asset" } }), which has no `url`. This renderer is correct and forward-ready
    // for the day the BFF also resolves embedded rich-text assets, but until then it is a
    // deliberate no-op (same visible behavior as before: nothing renders) rather than an
    // implicit unhandled-node-type fallthrough. Known gap — flag for the "what a non-PoC
    // build would add" list.
    [BLOCKS.EMBEDDED_ASSET]: (node) => {
      const target = (node.data as { target?: { url?: string; altText?: string; width?: number; height?: number } }).target;
      if (!target?.url) return null;

      return (
        <Image
          src={target.url}
          alt={target.altText ?? ""}
          width={target.width ?? 1200}
          height={target.height ?? 800}
          className="my-6 rounded"
        />
      );
    },
    [INLINES.HYPERLINK]: (node, children: ReactNode) => {
      const href = (node.data as { uri: string }).uri;
      const external = /^https?:\/\//.test(href);
      if (external) {
        return <a href={href} className="text-sky-700 underline" rel="noopener noreferrer" target="_blank">{children}</a>;
      }
      const internalHref = href.startsWith("/") ? href : `/${href}`;
      return <Link href={internalHref} className="text-sky-700 underline">{children}</Link>;
    },
  },
};

export function RichText({ document }: { document: unknown }) {
  if (!document || typeof document !== "object") return null;
  return <>{documentToReactComponents(document as Document, options)}</>;
}
