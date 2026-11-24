import type { Tag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { matchingKurztext } from '@/lib/matching'
import { tagTon } from './gemeinsam'

function sonderform(tag: Tag): string {
  if (tag.istSystem) return 'System'
  if (tag.istInbox) return 'Inbox-Tag'
  if (tag.istZahlungsTag) return 'Zahlungs-Tag'
  return '—'
}

/** Tabelle des Reiters „Tags“. Ohne `onBearbeiten` (NurLesen) fehlt die Spalte mit den Buttons. */
export function TagTabelle({
  tags,
  gewaehlt,
  onBearbeiten,
}: {
  tags: Tag[]
  gewaehlt?: string
  onBearbeiten?: (id: string) => void
}) {
  return (
    <TooltipProvider>
      <table className="tabelle min-w-[600px]">
        <thead>
          <tr>
            <th scope="col">Tag</th>
            <th scope="col">Sonderform</th>
            <th scope="col">Matching</th>
            <th scope="col" className="num">
              Belege
            </th>
            {onBearbeiten && (
              <th scope="col">
                <span className="sr-only">Bearbeiten</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {tags.map((t) => (
            <tr key={t.id} className={t.id === gewaehlt ? 'bg-[var(--color-akzent-zart)]' : undefined}>
              <td>
                <Chip ton={tagTon(t.farbe)}>{t.name}</Chip>
              </td>
              <td className="text-[13px]">{sonderform(t)}</td>
              <td className="text-[13px] text-gedaempft-stark">{t.istSystem ? '—' : matchingKurztext(t.matching)}</td>
              <td className="num">{t.anzahlBelege}</td>
              {onBearbeiten && (
                <td className="text-right">
                  {t.istSystem ? (
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <button
                          type="button"
                          aria-disabled="true"
                          aria-label={`„${t.name}“ ist ein System-Tag und kann nicht bearbeitet werden`}
                          className="inline-flex cursor-default border-0 bg-transparent p-1 text-gedaempft"
                        >
                          <Icon name="lock" />
                        </button>
                      </TooltipTrigger>
                      <TooltipContent>System-Tag – kann nicht bearbeitet werden</TooltipContent>
                    </Tooltip>
                  ) : (
                    <button
                      type="button"
                      className="btn btn-klein"
                      aria-label={`${t.name} bearbeiten`}
                      onClick={() => onBearbeiten(t.id)}
                    >
                      Bearbeiten
                    </button>
                  )}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </TooltipProvider>
  )
}
