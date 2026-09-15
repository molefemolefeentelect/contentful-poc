import { draftMode } from "next/headers";

export async function DraftModeBanner() {
  if (!(await draftMode()).isEnabled) return null;

  return (
    <div className="bg-amber-400 px-4 py-2 text-center text-sm font-semibold text-amber-950">
      Draft mode: you are seeing unpublished content.{" "}
      {/* Plain <a>, not next/link: this is a Route Handler (Task 5.1) that disables the
          draft cookie and redirects, not a Next.js page — it needs a real browser
          navigation, not client-side route interception. */}
      {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
      <a href="/api/draft/disable" className="underline">Exit preview</a>
    </div>
  );
}
