import Link from "next/link";
import type { NavigationItem, SiteSettings } from "@/lib/types";
import { RichText } from "@/components/RichText";

function FooterLink({ item }: { item: NavigationItem }) {
  return item.external ? (
    <a href={item.url} className="text-sm text-slate-600 hover:text-slate-900" rel="noopener noreferrer" target="_blank">
      {item.label}
    </a>
  ) : (
    <Link href={item.url} className="text-sm text-slate-600 hover:text-slate-900">
      {item.label}
    </Link>
  );
}

export function Footer({ settings }: { settings: SiteSettings | null }) {
  const nav = settings?.navigations.find((n) => n.key === "footer");

  return (
    <footer className="mt-12 border-t border-slate-200 bg-white">
      <div className="mx-auto max-w-6xl px-4 py-10">
        <nav aria-label="Footer">
          <ul className="flex flex-wrap gap-4">
            {nav?.items.map((item) => (
              <li key={item.label}><FooterLink item={item} /></li>
            ))}
          </ul>
        </nav>

        {settings && settings.socialLinks.length > 0 && (
          <ul className="mt-4 flex gap-4">
            {settings.socialLinks.map((link) => (
              <li key={link.label}>
                <a href={link.url} className="text-sm text-slate-600 hover:text-slate-900" rel="noopener noreferrer" target="_blank">
                  {link.label}
                </a>
              </li>
            ))}
          </ul>
        )}

        {settings?.disclaimerText != null && (
          <div className="mt-8 border-t border-slate-100 pt-6 text-xs text-slate-500">
            <RichText document={settings.disclaimerText} />
          </div>
        )}

        <p className="mt-6 text-xs text-slate-400">
          &copy; {new Date().getFullYear()} {settings?.siteName ?? "Fund Manager"}
        </p>
      </div>
    </footer>
  );
}
