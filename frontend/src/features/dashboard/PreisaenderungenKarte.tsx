import type { Preisaenderung } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { formatEuro, formatProzent } from '@/lib/format'
import { DashboardKarte } from './gemeinsam'

/** „Größte Preisänderungen“: Artikel mit altem und neuem Preis; gestiegen orange, gefallen grün. */
export function PreisaenderungenKarte({ eintraege }: { eintraege: Preisaenderung[] }) {
  return (
    <DashboardKarte titel="Größte Preisänderungen">
      {eintraege.length === 0 && <p className="gedaempft m-0 text-sm">Keine Preisänderungen.</p>}
      {eintraege.map((e) => (
        <div key={e.id} className="flex items-center gap-2.5 border-t border-[var(--color-linie)] py-2 text-sm">
          <span className="min-w-0 flex-1">
            <span className="block truncate font-medium">{e.name}</span>
            <span className="gedaempft block truncate text-xs">{e.korrespondent}</span>
          </span>
          <span className="num text-[13px]">
            {e.preisAlt != null ? formatEuro(e.preisAlt) : '–'} → {e.preisNeu != null ? formatEuro(e.preisNeu) : '–'}
          </span>
          <Chip ton={e.aenderung > 0 ? 'orange' : 'gruen'} icon={e.aenderung > 0 ? 'trending_up' : 'trending_down'}>
            {formatProzent(e.aenderung / 100, true)}
          </Chip>
        </div>
      ))}
    </DashboardKarte>
  )
}
