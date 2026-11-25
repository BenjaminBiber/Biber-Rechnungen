import { Link } from '@tanstack/react-router'
import { Bar, BarChart, CartesianGrid, LabelList, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { KorrespondentDetail } from '@/api/typen'
import { AenderungChip } from '@/features/artikel/AenderungChip'
import { TagChips } from '@/features/belege/liste/bausteine'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'

const monatsNamen = ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez']

function MonatTooltip({
  active,
  payload,
}: {
  active?: boolean
  payload?: Array<{ payload: { monat: string; wert: number } }>
}) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className="karte p-2 text-[13px] shadow-[var(--shadow-popover)]">
      {p.monat}: {formatEuro(p.wert)}
    </div>
  )
}

/** Karte „Kostenverlauf <Jahr>“: zwölf Monatsbalken mit Werten, Jahresauswahl und Tabelle für Screenreader. */
export function Kostenverlauf({
  detail,
  jahr,
  onJahr,
}: {
  detail: KorrespondentDetail
  jahr: number
  onJahr: (jahr: number) => void
}) {
  const aktuell = new Date().getFullYear()
  const jahre = Array.from(new Set([aktuell, aktuell - 1, aktuell - 2, jahr])).sort((a, b) => b - a)
  const daten = monatsNamen.map((monat, i) => ({ monat, wert: detail.kostenverlaufMonate[i] ?? 0 }))
  return (
    <section className="karte p-5" aria-labelledby="kostenverlauf-titel">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <h2 id="kostenverlauf-titel" className="m-0 text-base font-semibold">
          Kostenverlauf {jahr} <span className="gedaempft text-[13px] font-normal">brutto je Monat</span>
        </h2>
        <select
          aria-label="Jahr"
          className="eingabe w-auto"
          value={jahr}
          onChange={(e) => onJahr(Number(e.target.value))}
        >
          {jahre.map((j) => (
            <option key={j} value={j}>
              {j}
            </option>
          ))}
        </select>
      </div>
      <div className="h-[220px] min-w-0" aria-hidden="true">
        <ResponsiveContainer width="100%" height="100%" debounce={120}>
          <BarChart data={daten} margin={{ top: 18, right: 8, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--color-linie)" />
            <XAxis dataKey="monat" tickLine={false} axisLine={{ stroke: 'var(--color-schalter-aus)' }} fontSize={11} />
            <YAxis hide />
            <Tooltip content={<MonatTooltip />} cursor={{ fill: 'var(--color-hover)' }} />
            <Bar dataKey="wert" fill="var(--color-akzent)" radius={[4, 4, 0, 0]} isAnimationActive={false}>
              <LabelList
                dataKey="wert"
                position="top"
                fontSize={11}
                formatter={(v: unknown) => (Number(v) ? formatZahl(Number(v)) : '')}
              />
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
      <table className="sr-only">
        <caption>Kostenverlauf {jahr}</caption>
        <thead>
          <tr>
            <th scope="col">Monat</th>
            <th scope="col">Brutto</th>
          </tr>
        </thead>
        <tbody>
          {daten.map((d) => (
            <tr key={d.monat}>
              <th scope="row">{d.monat}</th>
              <td>{formatEuro(d.wert)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}

/** Karte „Belege“ mit den letzten Belegen und Link in die gefilterte Belegliste. */
export function BelegeKarte({ detail }: { detail: KorrespondentDetail }) {
  return (
    <section className="karte overflow-hidden" aria-labelledby="belege-titel">
      <div className="flex flex-wrap items-center justify-between gap-3 p-5 pb-3">
        <h2 id="belege-titel" className="m-0 text-base font-semibold">
          Belege
        </h2>
        <Link to="/belege" search={{ korrespondenten: [detail.id] }} className="text-[13px]">
          Alle {detail.kennzahlen.anzahlBelege} in der Belegliste
        </Link>
      </div>
      <div className="overflow-x-auto">
        <table className="tabelle" aria-labelledby="belege-titel">
          <thead>
            <tr>
              <th>Rechnungsnr.</th>
              <th>Datum</th>
              <th>Tags</th>
              <th className="num">Brutto</th>
            </tr>
          </thead>
          <tbody>
            {detail.letzteBelege.map((b) => (
              <tr key={b.id}>
                <td className="mono">
                  <Link to="/belege/$belegId" params={{ belegId: b.id }}>
                    {b.rechnungsnummer ?? 'ohne Nummer'}
                  </Link>
                </td>
                <td>{b.rechnungsdatum ? formatDatum(b.rechnungsdatum) : '–'}</td>
                <td>
                  <span className="flex flex-wrap gap-1">
                    <TagChips tags={b.tags} />
                  </span>
                </td>
                <td className="num">{b.brutto != null ? formatEuro(b.brutto) : '–'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

/** Karte „Artikel dieses Korrespondenten“ mit letztem Preis und Änderung seit dem ersten Kauf. */
export function ArtikelKarte({ detail }: { detail: KorrespondentDetail }) {
  return (
    <section className="karte overflow-hidden" aria-labelledby="artikel-titel">
      <div className="flex flex-wrap items-center justify-between gap-3 p-5 pb-3">
        <h2 id="artikel-titel" className="m-0 text-base font-semibold">
          Artikel dieses Korrespondenten
        </h2>
        <Link to="/artikel" search={{ korrespondentId: detail.id }} className="text-[13px]">
          Alle {detail.anzahlArtikel} Artikel
        </Link>
      </div>
      <div className="overflow-x-auto">
        <table className="tabelle" aria-labelledby="artikel-titel">
          <thead>
            <tr>
              <th>Artikel</th>
              <th>Stammartikel</th>
              <th className="num">Käufe</th>
              <th className="num">Letzter Preis</th>
              <th>Seit 1. Kauf</th>
            </tr>
          </thead>
          <tbody>
            {detail.artikel.map((a) => (
              <tr key={a.id}>
                <td>
                  <Link to="/artikel/$artikelId" params={{ artikelId: a.id }}>
                    {a.name}
                  </Link>
                </td>
                <td>{a.stammartikel?.name ?? <span className="gedaempft">—</span>}</td>
                <td className="num">{a.kaeufe}</td>
                <td className="num">
                  {a.letzterEinzelpreis != null
                    ? `${formatEuro(a.letzterEinzelpreis)}${a.einheit ? ` / ${a.einheit}` : ''}`
                    : '–'}
                </td>
                <td>
                  <AenderungChip prozent={a.aenderungSeitErstkauf} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
