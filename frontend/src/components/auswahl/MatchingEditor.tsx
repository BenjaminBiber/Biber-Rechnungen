import { useEffect, useId, useState } from 'react'
import { useMatchingTesten } from '@/api/tags'
import type { Matching, MatchingAlgorithmus } from '@/api/typen'
import { matchingAlgorithmen, ohneMuster } from '@/lib/matching'
import { useEntprellt } from './gemeinsam'
import { MatchingTestDialog, type MatchingZiel } from './MatchingTestDialog'

/** Matching-Block in den Dialogen für Tag, Kategorie und Korrespondent. */
export function MatchingEditor({
  wert,
  onAendern,
  ziel,
  zielId,
  zielName,
  onSpeichern,
}: {
  wert: Matching
  onAendern: (m: Matching) => void
  ziel: MatchingZiel
  zielId?: string
  zielName?: string
  /** „Regel speichern“ im Testdialog; ohne Angabe fehlt der Button. */
  onSpeichern?: () => void | Promise<void>
}) {
  const id = useId()
  const [testOffen, setTestOffen] = useState(false)
  const testen = useMatchingTesten()
  const { mutate } = testen
  const musterNoetig = !ohneMuster(wert.algorithmus)
  const pruefbar = wert.algorithmus !== 'Keiner' && (!musterNoetig || !!wert.muster?.trim())
  const entprellt = useEntprellt(JSON.stringify({ ziel, zielId: zielId ?? null, matching: wert }), 400)

  useEffect(() => {
    if (pruefbar) mutate(JSON.parse(entprellt))
  }, [entprellt, pruefbar, mutate])

  const ergebnis = pruefbar ? testen.data : undefined
  const testText = ergebnis
    ? `Trifft auf ${ergebnis.treffer} von ${ergebnis.gesamt} Belegen zu${
        ergebnis.letzterTreffer ? ` – zuletzt ${ergebnis.letzterTreffer}` : ''
      }.`
    : testen.isError && pruefbar
      ? 'Muster ungültig oder Test fehlgeschlagen.'
      : null

  return (
    <div className="grid gap-3">
      <div>
        <label className="label" htmlFor={`${id}-algorithmus`}>
          Algorithmus
        </label>
        <select
          id={`${id}-algorithmus`}
          className="eingabe"
          value={wert.algorithmus}
          onChange={(e) => onAendern({ ...wert, algorithmus: e.target.value as MatchingAlgorithmus })}
        >
          {matchingAlgorithmen.map((a) => (
            <option key={a.wert} value={a.wert}>
              {a.text}
            </option>
          ))}
        </select>
      </div>
      {musterNoetig && (
        <div>
          <label className="label" htmlFor={`${id}-muster`}>
            Muster
          </label>
          <input
            id={`${id}-muster`}
            className="eingabe mono"
            value={wert.muster ?? ''}
            onChange={(e) => onAendern({ ...wert, muster: e.target.value })}
          />
        </div>
      )}
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={wert.grossKlein}
          onChange={(e) => onAendern({ ...wert, grossKlein: e.target.checked })}
        />
        Groß-/Kleinschreibung beachten
      </label>
      <div className="flex items-center gap-3">
        <p className="hinweis flex-1" aria-live="polite">
          {testText}
        </p>
        <button type="button" className="btn btn-klein" disabled={!pruefbar} onClick={() => setTestOffen(true)}>
          Testen
        </button>
      </div>
      <MatchingTestDialog
        offen={testOffen}
        onOffenAendern={setTestOffen}
        wert={wert}
        ziel={ziel}
        zielId={zielId}
        zielName={zielName}
        onSpeichern={onSpeichern}
      />
    </div>
  )
}
