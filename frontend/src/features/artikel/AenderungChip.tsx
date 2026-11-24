import { Chip } from '@/components/Chip'
import { aenderungDarstellung } from './suche'

/** Preisänderung als Chip; der Ton ist zusätzlich als Text für Screenreader hinterlegt. */
export function AenderungChip({ prozent }: { prozent: number | null | undefined }) {
  if (prozent === null || prozent === undefined) return <span className="gedaempft">—</span>
  const { text, ton, label } = aenderungDarstellung(prozent)
  return (
    <Chip ton={ton}>
      {text}
      <span className="sr-only"> ({label})</span>
    </Chip>
  )
}
