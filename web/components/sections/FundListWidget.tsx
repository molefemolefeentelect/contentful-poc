import Link from "next/link";
import type { FundListSection } from "@/lib/types";

export function FundListWidget({ section }: { section: FundListSection }) {
  const isTable = section.displayVariant === "table";

  return (
    <section className="mx-auto max-w-6xl px-4 py-12">
      {section.heading && <h2 className="mb-2 text-2xl font-bold">{section.heading}</h2>}
      {section.intro && <p className="mb-6 max-w-2xl text-slate-600">{section.intro}</p>}

      {section.showFilters && section.categories.length > 0 && (
        <nav className="mb-6 flex flex-wrap gap-2" aria-label="Fund categories">
          {section.categories.map((category) => (
            <Link
              key={category.id}
              href={`/funds?categoryId=${category.id}`}
              className="rounded-full border border-slate-300 px-4 py-1.5 text-sm hover:border-sky-500 hover:text-sky-600"
            >
              {category.name}
            </Link>
          ))}
        </nav>
      )}

      {isTable ? (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <thead>
              <tr className="border-b border-slate-300 text-left">
                <th className="py-2 pr-4">Fund</th>
                <th className="py-2 pr-4">NAV</th>
                <th className="py-2 pr-4">Day change</th>
                <th className="py-2 pr-4">TER</th>
                <th className="py-2 pr-4">1yr return</th>
              </tr>
            </thead>
            <tbody>
              {section.funds.map((fund) => (
                <tr key={fund.code} className="border-b border-slate-100">
                  <td className="py-2 pr-4 font-medium">
                    <Link href={`/funds/${fund.code}`} className="text-sky-700 hover:underline">
                      {fund.name}
                    </Link>
                  </td>
                  <td className="py-2 pr-4">R{fund.nav.toFixed(2)}</td>
                  <td className="py-2 pr-4">{fund.dayChangePercent.toFixed(2)}%</td>
                  <td className="py-2 pr-4">{fund.ter.toFixed(2)}%</td>
                  <td className="py-2 pr-4">{fund.oneYearReturn.toFixed(1)}%</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          {section.funds.map((fund) => (
            <div key={fund.code} className="rounded border border-slate-200 p-4">
              <Link href={`/funds/${fund.code}`} className="font-semibold text-sky-700 hover:underline">
                {fund.name}
              </Link>
              <p className="mt-1 text-xs uppercase text-slate-500">{fund.code}</p>
              <dl className="mt-3 grid grid-cols-2 gap-y-1 text-sm">
                <dt className="text-slate-500">NAV</dt>
                <dd>R{fund.nav.toFixed(2)}</dd>
                <dt className="text-slate-500">Day change</dt>
                <dd>{fund.dayChangePercent.toFixed(2)}%</dd>
                <dt className="text-slate-500">TER</dt>
                <dd>{fund.ter.toFixed(2)}%</dd>
                <dt className="text-slate-500">1yr return</dt>
                <dd>{fund.oneYearReturn.toFixed(1)}%</dd>
              </dl>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
