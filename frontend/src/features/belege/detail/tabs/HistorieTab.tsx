import { useBelegHistorie } from '@/api/belege'
import type { AuditEintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatDatumZeit } from '@/lib/format'

const icons: Record<string, string> = {
  bearbeitet: 'edit',
  abgelegt: 'inventory',
  warnung: 'report',
  tags: 'sell',
  matching: 'auto_awesome',
  korrespondent: 'storefront',
  auslesen: 'document_scanner',
  import: 'input',
}

/** Farbe nach Herkunft: Warnung orange, System grau, Benutzer indigo. */
function farbe(e: AuditEintrag): string {
  if (e.art === 'warnung') return 'bg-orange-flaeche text-orange-text'
  return e.istSystem ? 'bg-grau-flaeche text-grau-text' : 'bg-akzent-zart text-akzent-text'
}

/** Tab „Historie“: unveränderliche Zeitleiste der Audit-Einträge. */
export function HistorieTab({ belegId }: { belegId: string }) {
  const { data: eintraege, isPending, isError } = useBelegHistorie(belegId)
  return (
    <div className="flex flex-col">
      <p className="gedaempft m-0 mb-3 text-[13px]">Lückenloses Änderungsprotokoll · nicht änderbar oder löschbar</p>
      {isPending && <p className="gedaempft m-0 text-[13px]">Historie wird geladen …</p>}
      {isError && <p className="m-0 text-[13px] text-rot-text">Historie konnte nicht geladen werden.</p>}
      <ol className="m-0 list-none p-0">
        {eintraege?.map((e) => (
          <li key={e.id} className="flex gap-3">
            <div className="flex flex-col items-center">
              <span className={`flex h-7 w-7 items-center justify-center rounded-full ${farbe(e)}`}>
                <Icon name={icons[e.art] ?? (e.istSystem ? 'settings' : 'edit')} className="text-base" />
              </span>
              <span className="min-h-3.5 w-0.5 flex-1 bg-rand" />
            </div>
            <div className="pb-3.5 text-[13px] leading-[1.45]">
              <div>
                <strong>{e.wer}</strong> <span className="gedaempft">· {formatDatumZeit(e.zeitpunkt)}</span>
              </div>
              <div>{e.text}</div>
              {(e.alterWert || e.neuerWert) && (
                <div className="mono mt-1">
                  <del className="text-rot-text">{e.alterWert ?? 'leer'}</del> →{' '}
                  <span className="text-gruen-text">{e.neuerWert ?? 'leer'}</span>
                </div>
              )}
            </div>
          </li>
        ))}
      </ol>
    </div>
  )
}
