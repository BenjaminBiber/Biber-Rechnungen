import type { Vorsteuer } from '@/api/typen'
import { formatEuro } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

const zeitraumText: Record<Vorsteuer['zeitraum'], string> = {
  Monatlich: 'je Monat',
  Quartalsweise: 'je Quartal',
  Jaehrlich: 'je Jahr',
}

/** „Vorsteuer <Jahr>“ je UStVA-Zeitraum; ohne Vorsteuer (Kleinunternehmer) wird nichts gerendert. */
export function VorsteuerKarte({ vorsteuer, jahr }: { vorsteuer: Vorsteuer | null | undefined; jahr: number }) {
  if (vorsteuer == null) return null
  return (
    <DashboardKarte
      titel={`Vorsteuer ${jahr}`}
      zusatz={<span className="gedaempft">{zeitraumText[vorsteuer.zeitraum]}</span>}
    >
      {vorsteuer.zeilen.map((z) => (
        <div
          key={z.bezeichnung}
          className="flex justify-between gap-2.5 border-t border-[var(--color-linie)] py-2 text-sm"
        >
          <span>{z.bezeichnung}</span>
          <span className="num">{formatEuro(z.betrag)}</span>
        </div>
      ))}
      <div className="flex justify-between gap-2.5 border-t border-[var(--color-rand)] pt-2 text-sm font-semibold">
        <span>Summe</span>
        <span className="num">{formatEuro(vorsteuer.summe)}</span>
      </div>
    </DashboardKarte>
  )
}
