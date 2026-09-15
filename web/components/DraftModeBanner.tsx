import { draftMode } from "next/headers";

export async function DraftModeBanner() {
  if (!(await draftMode()).isEnabled) return null;

  return (
    <div className="bg-amber-400 px-4 py-2 text-center text-sm font-semibold text-amber-950">
      Draft mode: you are seeing unpublished content.{" "}
      <a href="/api/draft/disable" className="underline">Exit preview</a>
    </div>
  );
}
