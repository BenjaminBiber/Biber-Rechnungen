import { useEffect, useId, useState } from 'react'
import { useMatchingAnwenden, useMatchingTesten } from '@/api/tags'
import type { Matching } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { LadeBereich, Skelett } from '@/components/ui/skelett'
import { Dialog, DialogContent, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { matchingKurztext, ohneMuster } from '@/lib/matching'

export type MatchingZiel = 'Tag' | 'Kategorie' | 'Korrespondent'

const ohneText: Record<MatchingZiel, string> = {
  Tag: 'ohne den Tag',
  Kategorie: 'ohne die Kategorie',
  Korrespondent: 'ohne den Korrespondenten',
}

/** Auszug mit `<mark>` um den Treffer. */
function Auszug({ text, start, laenge }: { text: string; start?: number | null; laenge?: number | null }) {
  if (start == null || !laenge) return <>{text}</>
  return (
    <>
      {text.slice(0, start)}
      <mark>{text.slice(start, start + laenge)}</mark>
      {text.slice(start + laenge)}
    </>
  )
}

/** Dialog „Matching-Regel testen“ – aus dem Matching-Editor über „Testen“. */
export function MatchingTestDialog({
  offen,
  onOffenAendern,
  wert,
  ziel,
  zielId,
  zielName,
  onSpeichern,
}: {
  offen: boolean
  onOffenAendern: (offen: boolean) => void
  wert: Matching
  ziel: MatchingZiel
  zielId?: string
  zielName?: string
  onSpeichern?: () => void | Promise<void>
}) {
  const checkboxId = useId()
  const testen = useMatchingTesten()
  const anwenden = useMatchingAnwenden()
  const [auchBestehende, setAuchBestehende] = useState(false)
  const [speichert, setSpeichert] = useState(false)
  const { mutate } = testen

  useEffect(() => {
    if (offen) mutate({ ziel, zielId: zielId ?? null, matching: wert })
  }, [offen, mutate, ziel, zielId, wert])

  const ergebnis = testen.data
  const ohne = ergebnis?.ohneZuweisung ?? 0

  async function speichern() {
    setSpeichert(true)
    try {
      await onSpeichern?.()
      if (auchBestehende && zielId && ohne > 0) await anwenden.mutateAsync({ ziel, zielId })
      onOffenAendern(false)
    } finally {
      setSpeichert(false)
    }
  }

  return (
    <Dialog open={offen} onOpenChange={onOffenAendern}>
      <DialogContent>
        <DialogTitle>{`Regel testen: „${zielName ?? ''}“`}</DialogTitle>
        <div className="flex items-center gap-2">
          <Chip ton="blau">{matchingKurztext({ ...wert, muster: null })}</Chip>
          {!ohneMuster(wert.algorithmus) && wert.muster && <span className="mono">{wert.muster}</span>}
        </div>
        {ergebnis ? (
          <>
            {ergebnis.auszug && (
              <p className="rounded-md bg-flaeche p-3 text-sm" data-testid="auszug">
                <Auszug text={ergebnis.auszug} start={ergebnis.trefferStart} laenge={ergebnis.trefferLaenge} />
              </p>
            )}
            <p>
              <strong>{`${ergebnis.treffer} Treffer in ${ergebnis.gesamt} Belegen`}</strong>
              {ohne > 0 && <span className="gedaempft">{` · davon ${ohne} ${ohneText[ziel]}`}</span>}
            </p>
            {ohne > 0 && zielId && (
              <div className="flex items-center gap-2">
                <input
                  id={checkboxId}
                  type="checkbox"
                  checked={auchBestehende}
                  onChange={(e) => setAuchBestehende(e.target.checked)}
                />
                <label htmlFor={checkboxId}>{`${ziel} jetzt auch auf die ${ohne} bestehenden Belege anwenden`}</label>
              </div>
            )}
          </>
        ) : testen.isError ? (
          <p className="gedaempft">Test fehlgeschlagen.</p>
        ) : (
          <LadeBereich label="Regel wird getestet" className="flex flex-col gap-3">
            <Skelett hoehe={48} className="w-full rounded-md" />
            <Skelett hoehe={14} breite="55%" />
          </LadeBereich>
        )}
        <DialogFooter>
          <button type="button" className="btn" onClick={() => onOffenAendern(false)}>
            Schließen
          </button>
          {onSpeichern && (
            <button
              type="button"
              className="btn btn-primaer"
              disabled={speichert || anwenden.isPending}
              aria-busy={speichert || anwenden.isPending || undefined}
              onClick={() => void speichern()}
            >
              Regel speichern
            </button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
