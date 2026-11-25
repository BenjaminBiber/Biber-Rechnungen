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
                <span className="inline-flex items-center gap-1">
                  <Chip ton={tagTon(t.farbe)}>{t.name}</Chip>
                  {t.istSystem && (
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <button
                          type="button"
                          aria-label={`Hinweis zum System-Tag „${t.name}“`}
                          className="inline-flex cursor-help border-0 bg-transparent p-0.5 text-gedaempft hover:text-text"
                        >
                          <Icon name="info" className="text-[16px] [vertical-align:0]" />
                        </button>
                      </TooltipTrigger>
                      <TooltipContent className="max-w-[280px]">
                        „{t.name}“ ist ein System-Tag: Ihn setzt nur die Pipeline, entfernt wird er nur durch
                        „Bestätigen“. Er kann nicht bearbeitet oder gelöscht werden.
                      </TooltipContent>
                    </Tooltip>
                  )}
                </span>
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
