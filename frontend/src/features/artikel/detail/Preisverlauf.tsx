import { CartesianGrid, LabelList, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import type { PreisDetail } from '@/api/typen'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'
import { einheitKurz, grundpreisBezug, type Preisart } from './gemeinsam'

const monatsNamen = ['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez']
const farben = ['var(--color-akzent)', 'var(--color-orange-text)', 'var(--color-petrol-text)']
const striche = [undefined, '6 4', '2 3']

type Punkt = { t: number; datum: string; wert: number; einzelpreis?: number | null; grundpreis?: number | null }

type Serie = { name: string; legende: string; punkte: Punkt[] }

const zeit = (datum: string) => new Date(`${datum}T00:00:00Z`).getTime()

function PunktTooltip({ active, payload }: { active?: boolean; payload?: Array<{ payload: Punkt; name?: string }> }) {
  if (!active || !payload?.length) return null
  const p = payload[0].payload
  return (
    <div className="karte p-3 text-[13px] shadow-[var(--shadow-popover)]">
      <div className="mb-1 font-semibold">
        {payload[0].name} · {formatDatum(p.datum)}
      </div>
      {p.einzelpreis != null && <div>Einzelpreis: {formatEuro(p.einzelpreis)}</div>}
      {p.grundpreis != null && <div>Grundpreis: {formatEuro(p.grundpreis)}</div>}
    </div>
  )
}

/** Serien der gewählten Preisart; Einzelpreise nur in der Verpackungseinheit der ersten Serie. */
function serienAus(detail: PreisDetail, preisart: Preisart): Serie[] {
  const einheit = detail.serien[0]?.einheit
  return detail.serien
    .filter((s) => preisart === 'grund' || s.einheit === einheit)
    .map((s) => ({
      name: s.korrespondent.name,
      legende:
        preisart === 'grund'
          ? `${s.korrespondent.name} · je ${grundpreisBezug(detail)}`
          : `${s.korrespondent.name} · ${s.einheit ?? ''}${
              s.umrechnungFaktor != null && detail.basiseinheit
                ? ` à ${formatZahl(s.umrechnungFaktor)} ${einheitKurz(detail.basiseinheit)}`
                : ''
            }`,
      punkte: s.punkte.flatMap((p) => {
        const wert = preisart === 'grund' ? p.grundpreis : p.einzelpreis
        return wert == null
          ? []
          : [{ t: zeit(p.datum), datum: p.datum, wert, einzelpreis: p.einzelpreis, grundpreis: p.grundpreis }]
      }),
    }))
}

/** Liniendiagramm „Preisverlauf <Jahr>“ mit Umschalter Grund-/Einzelpreis und Textalternative. */
export function Preisverlauf({
  detail,
  jahr,
  preisart,
  mitGrundpreis,
  onPreisart,
}: {
  detail: PreisDetail
  jahr: number
  preisart: Preisart
  mitGrundpreis: boolean
  onPreisart: (preisart: Preisart) => void
}) {
  const serien = serienAus(detail, preisart)
  const art = preisart === 'grund' ? 'Grundpreis' : 'Einzelpreis'
  const beschreibung = `Preisverlauf ${jahr}, ${art}: ${serien
    .map((s) => {
      const erster = s.punkte[0]
      const letzter = s.punkte[s.punkte.length - 1]
      return erster
        ? `${s.name} von ${formatEuro(erster.wert)} auf ${formatEuro(letzter.wert)}`
        : `${s.name} ohne Preise`
    })
    .join('; ')}`
  const ticks = Array.from({ length: 12 }, (_, m) => Date.UTC(jahr, m, 1))

  return (
    <section className="karte p-5" aria-labelledby="preisverlauf-titel">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <h2 id="preisverlauf-titel" className="m-0 text-base font-semibold">
          Preisverlauf {jahr}
        </h2>
        <div className="segment" role="group" aria-label="Preisart">
          {mitGrundpreis && (
            <button type="button" aria-pressed={preisart === 'grund'} onClick={() => onPreisart('grund')}>
              Grundpreis je {grundpreisBezug(detail)}
            </button>
          )}
          <button type="button" aria-pressed={preisart === 'einzel'} onClick={() => onPreisart('einzel')}>
            Einzelpreis je {detail.serien[0]?.einheit ?? 'Stück'}
          </button>
        </div>
      </div>
      {preisart === 'einzel' && detail.serien.length > 1 && (
        <p className="gedaempft m-0 mb-2 text-[13px]">
          Einzelpreise sind nur innerhalb gleicher Verpackungseinheit vergleichbar.
        </p>
      )}
      <div className="h-[260px] min-w-0" role="img" aria-label={beschreibung}>
        <ResponsiveContainer width="100%" height="100%">
          <LineChart margin={{ top: 18, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid vertical={false} stroke="var(--color-linie)" />
            <XAxis
              type="number"
              dataKey="t"
              domain={[ticks[0], Date.UTC(jahr, 11, 31)]}
              ticks={ticks}
              tickFormatter={(t: number) => monatsNamen[new Date(t).getUTCMonth()]}
              tickLine={false}
              axisLine={{ stroke: 'var(--color-schalter-aus)' }}
              fontSize={11}
            />
            <YAxis
              tickFormatter={(v: number) => formatZahl(v, 2)}
              tickLine={false}
              axisLine={false}
              fontSize={11}
              width={48}
              domain={['auto', 'auto']}
            />
            <Tooltip content={<PunktTooltip />} />
            {serien.map((s, i) => (
              <Line
                key={s.name}
                data={s.punkte}
                dataKey="wert"
                name={s.name}
                stroke={farben[i % farben.length]}
                strokeDasharray={striche[i % striche.length]}
                strokeWidth={2}
                dot={{ r: 3 }}
                isAnimationActive={false}
              >
                <LabelList
                  dataKey="wert"
                  position="top"
                  fontSize={11}
                  formatter={(v: unknown) => formatZahl(Number(v), 2)}
                />
              </Line>
            ))}
          </LineChart>
        </ResponsiveContainer>
      </div>
      <ul
        className="m-0 mt-2 flex list-none flex-wrap gap-x-4 gap-y-1 p-0 text-xs text-[var(--color-gedaempft-stark)]"
        aria-label="Legende"
      >
        {serien.map((s, i) => (
          <li key={s.name} className="flex items-center gap-1.5">
            <svg width="22" height="6" aria-hidden="true">
              <line
                x1="0"
                y1="3"
                x2="22"
                y2="3"
                stroke={farben[i % farben.length]}
                strokeWidth="2"
                strokeDasharray={striche[i % striche.length]}
              />
            </svg>
            {s.legende}
          </li>
        ))}
      </ul>
      <table className="sr-only">
        <caption>
          Preisverlauf {jahr}, {art}
        </caption>
        <thead>
          <tr>
            <th scope="col">Korrespondent</th>
            <th scope="col">Datum</th>
            <th scope="col">{art}</th>
          </tr>
        </thead>
        <tbody>
          {serien.flatMap((s) =>
            s.punkte.map((p) => (
              <tr key={`${s.name}-${p.datum}`}>
                <th scope="row">{s.name}</th>
                <td>{formatDatum(p.datum)}</td>
                <td>{formatEuro(p.wert)}</td>
              </tr>
            )),
          )}
        </tbody>
      </table>
    </section>
  )
}
