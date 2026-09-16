import { draftMode } from "next/headers";
import { redirect } from "next/navigation";
import type { NextRequest } from "next/server";

/**
 * Target of Contentful's preview URL.
 *
 * Draft mode is enabled HERE, not in the BFF: the cookie must be set on the Next.js
 * origin to be sent back with subsequent page requests. The BFF validates the secret
 * and owns the Contentful Preview API token.
 */
// A same-origin, single-leading-slash path. Rejects protocol-relative ("//host") and
// backslash-based ("\host", browser-normalized to "//host") open-redirect payloads.
const isSafeRedirectPath = (value: unknown): value is string =>
  typeof value === "string" && (value === "/" || /^\/[^/\\]/.test(value));

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const secret = searchParams.get("secret");
  const slug = searchParams.get("slug") ?? "";

  const bff = process.env.BFF_BASE_URL ?? "http://localhost:5080";

  let redirectTo: unknown;
  try {
    const response = await fetch(
      `${bff}/api/preview?secret=${encodeURIComponent(secret ?? "")}&slug=${encodeURIComponent(slug)}`,
      { cache: "no-store" },
    );

    if (!response.ok) {
      return new Response("Invalid preview token.", { status: 401 });
    }

    ({ redirectTo } = (await response.json()) as { redirectTo?: unknown });
  } catch (error) {
    console.error("Failed to reach the BFF's preview endpoint.", error);
    return new Response("Preview service unavailable.", { status: 503 });
  }

  if (!isSafeRedirectPath(redirectTo)) {
    console.error("BFF returned an unsafe or missing redirectTo for preview:", redirectTo);
    return new Response("Invalid preview token.", { status: 401 });
  }

  (await draftMode()).enable();
  redirect(redirectTo);
}
