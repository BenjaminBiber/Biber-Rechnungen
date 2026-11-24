import { useState } from 'react'
import { belegVorschauUrl } from '@/api/belege'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { gruppeGrau } from './bausteine'

/** Große Vorschau eines Belegs; bei mehreren Seiten wird geblättert. */
export function VorschauDialog({
  beleg,
  onSchliessen,
}: {
  beleg: BelegListeneintrag | null
  onSchliessen: () => void
}) {
  return (
    <Dialog open={beleg !== null} onOpenChange={(offen) => !offen && onSchliessen()}>
      {beleg && <VorschauInhalt key={beleg.id} beleg={beleg} />}
    </Dialog>
  )
}

function VorschauInhalt({ beleg }: { beleg: BelegListeneintrag }) {
  const [seite, setSeite] = useState(1)
  const seiten = Math.max(1, beleg.seitenanzahl)
  const titel = [beleg.korrespondent?.name, beleg.titel].filter(Boolean).join(': ')

  return (
    <DialogContent className="max-w-[760px]">
      <DialogHeader>
        <DialogTitle>Vorschau</DialogTitle>
        <DialogDescription>{titel}</DialogDescription>
      </DialogHeader>
      <div className="flex justify-center rounded-lg bg-segment p-4">
        <img
          src={belegVorschauUrl(beleg.id, seite)}
          alt={`Seite ${seite} von ${seiten}`}
          className="max-h-[70vh] max-w-full bg-white object-contain shadow-blatt"
        />
      </div>
      {seiten > 1 && (
        <div className="flex items-center justify-center gap-3 text-sm">
          <div className={gruppeGrau}>
            <button
              type="button"
              aria-label="Vorherige Seite"
              disabled={seite <= 1}
              onClick={() => setSeite(seite - 1)}
              className="!w-8 !px-0"
            >
              <Icon name="chevron_left" />
            </button>
            <button
              type="button"
              aria-label="Nächste Seite"
              disabled={seite >= seiten}
              onClick={() => setSeite(seite + 1)}
              className="!w-8 !px-0"
            >
              <Icon name="chevron_right" />
            </button>
          </div>
          <span aria-live="polite">
            Seite {seite} von {seiten}
          </span>
        </div>
      )}
    </DialogContent>
  )
}
