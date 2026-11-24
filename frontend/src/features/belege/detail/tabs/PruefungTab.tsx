import type { BelegDetail, PruefErgebnis } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'

const darstellung: Record<PruefErgebnis, { icon: string; farbe: string; ton: ChipTon; text: string; rang: number }> = {
  Fehlgeschlagen: { icon: 'report', farbe: 'text-orange-text', ton: 'orange', text: 'fehlgeschlagen', rang: 0 },
  Ok: { icon: 'check_circle', farbe: 'text-gruen-text', ton: 'gruen', text: 'ok', rang: 1 },
  NichtAnwendbar: {
    icon: 'remove_circle_outline',
    farbe: 'text-gedaempft',
    ton: 'grau',
    text: 'nicht anwendbar',
    rang: 2,
  },
}

/** Tab „Prüfung“: alle Prüfbefunde, fehlgeschlagene zuerst. */
export function PruefungTab({ beleg }: { beleg: BelegDetail }) {
  if (beleg.befunde.length === 0) return <p className="gedaempft m-0 text-sm">Noch nicht geprüft</p>
  const befunde = [...beleg.befunde].sort((a, b) => darstellung[a.ergebnis].rang - darstellung[b.ergebnis].rang)
  return (
    <ul className="m-0 flex list-none flex-col p-0">
      {befunde.map((b) => {
        const d = darstellung[b.ergebnis]
        return (
          <li key={b.pruefung} className="flex gap-3 border-b border-linie py-3">
            <Icon name={d.icon} className={d.farbe} />
            <span className="flex flex-1 flex-col gap-0.5">
              <span className="text-sm font-medium">{b.name}</span>
              <span className="gedaempft text-[13px]">{b.detail}</span>
            </span>
            <span>
              <Chip ton={d.ton}>{d.text}</Chip>
            </span>
          </li>
        )
      })}
    </ul>
  )
}
