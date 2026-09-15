import type { FundDetailSection } from "@/lib/types";

export function FundDetailWidget({ section }: { section: FundDetailSection }) {
  if (section.unavailable) {
    return (
      <section className="mx-auto max-w-4xl px-4 py-12">
        <p className="text-slate-600">Fund data is temporarily unavailable.</p>
      </section>
    );
  }

  const dayChange = section.dayChangePercent ?? null;
  const dayChangeClass = dayChange === null ? "" : dayChange >= 0 ? "text-green-600" : "text-red-600";

  const priceCount = section.prices.length;
  const priceRange =
    priceCount > 0
      ? `${section.prices[0].date} – ${section.prices[priceCount - 1].date}`
      : null;

  return (
    <section className="mx-auto max-w-4xl px-4 py-12">
      <h2 className="text-2xl font-bold">{section.name}</h2>
      <p className="mt-1 text-sm uppercase tracking-wide text-slate-500">
        {section.code} {section.isin && `· ${section.isin}`}
      </p>
      {section.shortDescription && <p className="mt-4 text-slate-600">{section.shortDescription}</p>}

      <dl className="mt-6 grid grid-cols-2 gap-y-2 text-sm sm:grid-cols-4">
        <dt className="text-slate-500">NAV</dt>
        <dd>{section.nav != null ? `R${section.nav.toFixed(2)}` : "—"}</dd>
        <dt className="text-slate-500">Day change</dt>
        <dd className={dayChangeClass}>{dayChange != null ? `${dayChange.toFixed(2)}%` : "—"}</dd>
        <dt className="text-slate-500">TER</dt>
        <dd>{section.ter != null ? `${section.ter.toFixed(2)}%` : "—"}</dd>
        <dt className="text-slate-500">Inception date</dt>
        <dd>{section.inceptionDate ?? "—"}</dd>
      </dl>

      {section.performance && (
        <table className="mt-8 w-full max-w-md border-collapse text-sm">
          <thead>
            <tr className="border-b border-slate-300 text-left">
              <th className="py-2 pr-4">1yr</th>
              <th className="py-2 pr-4">3yr</th>
              <th className="py-2 pr-4">5yr</th>
              <th className="py-2 pr-4">Since inception</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td className="py-2 pr-4">{section.performance.oneYear.toFixed(1)}%</td>
              <td className="py-2 pr-4">{section.performance.threeYear.toFixed(1)}%</td>
              <td className="py-2 pr-4">{section.performance.fiveYear.toFixed(1)}%</td>
              <td className="py-2 pr-4">{section.performance.sinceInception.toFixed(1)}%</td>
            </tr>
          </tbody>
        </table>
      )}

      {priceRange && (
        <p className="mt-6 text-sm text-slate-500">
          {priceCount} price {priceCount === 1 ? "point" : "points"} on record ({priceRange}).
        </p>
      )}

      {section.factsheetUrl && (
        <a href={section.factsheetUrl} className="mt-6 inline-block text-sky-700 underline" rel="noopener noreferrer" target="_blank">
          Download factsheet
        </a>
      )}
    </section>
  );
}
