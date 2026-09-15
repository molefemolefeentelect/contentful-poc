import type { ComponentType } from "react";
import type { Section } from "@/lib/types";
import { sectionRegistry } from "./registry";

export function SectionRenderer({ sections }: { sections: Section[] }) {
  return (
    <>
      {sections.map((section) => {
        const Component = sectionRegistry[section.__type] as ComponentType<{ section: Section }> | undefined;

        if (!Component) {
          // Degradation contract, matching the BFF: a block type this build does not
          // know about is skipped, never fatal. The BFF drops unknown types already;
          // this guards against a frontend deployed behind a newer content model.
          if (process.env.NODE_ENV === "development") {
            return (
              <div key={section.id} className="mx-auto my-4 max-w-4xl border border-dashed border-amber-500 bg-amber-50 p-4 text-sm text-amber-900">
                No component registered for section type <code className="font-mono">{section.__type}</code>.
                Add it to <code className="font-mono">components/sections/registry.ts</code>.
              </div>
            );
          }
          return null;
        }

        return <Component key={section.id} section={section} />;
      })}
    </>
  );
}
