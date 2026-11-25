import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useAlleBenachrichtigungenGelesen, useBenachrichtigungen } from '@/api/benachrichtigungen'
import { Seitenkopf } from '@/components/Seitenkopf'
import { BenachrichtigungZeile, useBenachrichtigungOeffnen } from '@/components/shell/Glocke'

export const Route = createFileRoute('/_app/benachrichtigungen')({
  component: Benachrichtigungen,
})

/** Vollständige, blätterbare Liste aller Benachrichtigungen mit Filter „nur ungelesene“. */
function Benachrichtigungen() {
  const [nurUngelesen, setNurUngelesen] = useState(false)
  const [seite, setSeite] = useState(1)
  const { data, isPending } = useBenachrichtigungen({ nurUngelesen: nurUngelesen || undefined, seite })
  const alleGelesen = useAlleBenachrichtigungenGelesen()
  const oeffnen = useBenachrichtigungOeffnen()
  const seiten = data ? Math.max(1, Math.ceil(data.gesamt / data.seitengroesse)) : 1

  return (
    <main className="seite">
      <Seitenkopf
        titel="Benachrichtigungen"
        untertitel={data && `${data.gesamt} Einträge`}
        aktionen={
          <button type="button" className="btn" onClick={() => alleGelesen.mutate()}>
            Alle gelesen
          </button>
        }
      />
      <label className="mb-4 flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={nurUngelesen}
          onChange={(e) => {
            setNurUngelesen(e.target.checked)
            setSeite(1)
          }}
        />
        nur ungelesene
      </label>
      <div className="karte overflow-hidden">
        {isPending && <p className="gedaempft m-0 p-6 text-center text-sm">Wird geladen …</p>}
        {data?.eintraege.length === 0 && (
          <p className="gedaempft m-0 p-6 text-center text-sm">Keine Benachrichtigungen</p>
        )}
        {data?.eintraege.map((n) => (
          <BenachrichtigungZeile key={n.id} eintrag={n} beiKlick={oeffnen} />
        ))}
      </div>
      {seiten > 1 && (
        <nav aria-label="Seiten" className="mt-4 flex items-center justify-end gap-2 text-sm">
          <button type="button" className="btn btn-klein" disabled={seite <= 1} onClick={() => setSeite(seite - 1)}>
            Zurück
          </button>
          <span>
            Seite {seite} von {seiten}
          </span>
          <button
            type="button"
            className="btn btn-klein"
            disabled={seite >= seiten}
            onClick={() => setSeite(seite + 1)}
          >
            Weiter
          </button>
        </nav>
      )}
    </main>
  )
}
