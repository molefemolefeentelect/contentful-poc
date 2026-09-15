import Link from "next/link";
import type { NavigationItem, SiteSettings } from "@/lib/types";

function NavLink({ item }: { item: NavigationItem }) {
  if (item.children.length > 0) {
    // Server-rendered disclosure: children are in the DOM for crawlers even when closed.
    return (
      <details className="group relative">
        <summary className="cursor-pointer list-none px-3 py-2 font-medium hover:text-sky-700">{item.label}</summary>
        <ul className="absolute left-0 z-10 min-w-48 rounded border border-slate-200 bg-white p-2 shadow-lg">
          {item.children.map((child) => (
            <li key={child.label}>
              <NavLink item={child} />
            </li>
          ))}
        </ul>
      </details>
    );
  }

  return item.external ? (
    <a href={item.url} className="block px-3 py-2 font-medium hover:text-sky-700" rel="noopener noreferrer" target="_blank">
      {item.label}
    </a>
  ) : (
    <Link href={item.url} className="block px-3 py-2 font-medium hover:text-sky-700">
      {item.label}
    </Link>
  );
}

export function Header({ settings }: { settings: SiteSettings | null }) {
  const nav = settings?.navigations.find((n) => n.key === "header");

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-4">
        <Link href="/" className="text-lg font-bold">{settings?.siteName ?? "Fund Manager"}</Link>
        <nav aria-label="Main">
          <ul className="flex items-center gap-1">
            {nav?.items.map((item) => (
              <li key={item.label}><NavLink item={item} /></li>
            ))}
          </ul>
        </nav>
      </div>
    </header>
  );
}
