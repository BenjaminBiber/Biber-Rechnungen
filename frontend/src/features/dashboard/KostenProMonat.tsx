import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { MonatsKosten } from '@/api/typen'
import { formatEuro, formatZahl } from '@/lib/format'
import { DashboardKarte, monatsIndex, monatsNamen } from './gemeinsam'

type Kategorie = { id: string; name: string; farbe: string }

type Zeile = Record<string, number | string> & { name: string; summe: number; summeVorjahr: number }

type Anteil = MonatsKosten['nachKategorie'][number]

/** Schlüssel einer Kategorie im Diagramm; Beträge ohne Kategorie landen unter „ohne“. */
const schluessel = (a: Anteil) => a.kategorie?.id ?? 'ohne'

/** Kategorien in der Reihenfolge ihres ersten Auftretens. */
function kategorienAus(monate: MonatsKosten[]): Kategorie[] {
  const gesehen = new Map<string, Kategorie>()
  for (const m of monate) {
    for (const a of m.nachKategorie) {
      const id = schluessel(a)
      if (!gesehen.has(id)) gesehen.set(id, { id, name: a.kategorie?.name ?? 'Ohne Kategorie', farbe: a.farbe })
    }
  }
  return [...gesehen.values()]
}

function KostenTooltip({
  active,
  payload,
  kategorien,
}: {
  active?: boolean
  payload?: Array<{ payload: Zeile }>
  kategorien: Kategorie[]
}) {
  if (!active || !payload?.length) return null
  const zeile = payload[0].payload
  return (
    <div className="karte p-3 text-[13px] shadow-[var(--shadow-popover)]">
      <div className="mb-1 font-semibold">
        {zeile.name}: {formatEuro(zeile.summe)}
      </div>
      {kategorien
        .filter((k) => Number(zeile[k.id] ?? 0) > 0)
        .map((k) => (
          <div key={k.id} className="flex items-center gap-2">
            <span className="inline-block size-2.5 rounded-sm" style={{ background: k.farbe }} />
            <span className="flex-1">{k.name}</span>
            <span className="num">{formatEuro(Number(zeile[k.id]))}</span>
          </div>
        ))}
      <div className="gedaempft mt-1">Vorjahr: {formatEuro(zeile.summeVorjahr)}</div>
    </div>
  )
}

/** Balkendiagramm „Gesamtkosten pro Monat“, gestapelt nach Kategorie, mit Tabelle für Screenreader. */
export function KostenProMonat({ monate, jahr }: { monate: MonatsKosten[]; jahr: number }) {
  const kategorien = kategorienAus(monate)
  const zeilen: Zeile[] = monate.map((m) => {
    const zeile: Zeile = { name: monatsNamen[monatsIndex(m.monat)], summe: m.summe, summeVorjahr: m.summeVorjahr ?? 0 }
    for (const a of m.nachKategorie) zeile[schluessel(a)] = a.summe
    return zeile
  })
  const maximum = Math.max(1000, ...zeilen.map((z) => z.summe))
  const ticks = Array.from({ length: Math.ceil(maximum / 1000) + 1 }, (_, i) => i * 1000)

  return (
    <DashboardKarte
      titel="Gesamtkosten pro Monat"
      zusatz={<span className="gedaempft">{jahr} · brutto</span>}
      className="flex-[2_1_560px]"
    >
      <div className="h-[260px] min-w-0" aria-hidden="true">
        <ResponsiveContainer width="100%" height="100%" debounce={120}>
          <BarChart data={zeilen} margin={{ top: 18, right: 0, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--color-linie)" />
            <XAxis dataKey="name" tickLine={false} axisLine={{ stroke: 'var(--color-schalter-aus)' }} fontSize={11} />
            <YAxis
              ticks={ticks}
              domain={[0, ticks[ticks.length - 1]]}
              tickFormatter={(v: number) => formatZahl(v, 0)}
              tickLine={false}
              axisLine={false}
              fontSize={11}
              width={44}
            />
            <Tooltip content={<KostenTooltip kategorien={kategorien} />} cursor={{ fill: 'var(--color-hover)' }} />
            {kategorien.map((k, i) => (
              <Bar key={k.id} dataKey={k.id} name={k.name} stackId="kosten" fill={k.farbe} maxBarSize={56}>
                {i === kategorien.length - 1 && (
                  <LabelList
                    dataKey="summe"
                    position="top"
                    fontSize={11}
                    fill="var(--color-gedaempft-stark)"
                    formatter={(v: unknown) => (Number(v) > 0 ? formatZahl(Number(v), 0) : '')}
                  />
                )}
              </Bar>
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
      <ul className="m-0 flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-xs text-[var(--color-gedaempft-stark)]">
        {kategorien.map((k) => (
          <li key={k.id} className="flex items-center gap-1.5">
            <span className="inline-block size-2.5 rounded-sm" style={{ background: k.farbe }} />
            {k.name}
          </li>
        ))}
      </ul>
      <table className="sr-only">
        <caption>Gesamtkosten pro Monat {jahr}</caption>
        <thead>
          <tr>
            <th scope="col">Monat</th>
            <th scope="col">Summe</th>
            <th scope="col">Vorjahr</th>
          </tr>
        </thead>
        <tbody>
          {zeilen.map((z) => (
            <tr key={z.name}>
              <th scope="row">{z.name}</th>
              <td>{formatEuro(z.summe)}</td>
              <td>{formatEuro(z.summeVorjahr)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </DashboardKarte>
  )
}
