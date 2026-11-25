import { Fragment, useState } from 'react'
import { belegVorschauUrl, useAufteilen } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegDetail } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogTitle } from '@/components/ui/dialog'
import { istPdf, original } from '../detail/gemeinsam'
import { seitenText, teilBuchstabe, teileAusSchnitten } from './schnitte'

/** Wechselnde Töne der Teile: Chip-Klasse und Rahmenfarbe der Miniaturen. */
const toene = [
  { chip: 'chip-blau', rand: 'var(--color-blau-text)' },
  { chip: 'chip-orange', rand: 'var(--color-orange-text)' },
  { chip: 'chip-gruen', rand: 'var(--color-gruen-text)' },
  { chip: 'chip-lila', rand: 'var(--color-lila-text)' },
  { chip: 'chip-petrol', rand: 'var(--color-petrol-text)' },
]

/** Dateiname des Originals (letzter Pfadteil), sonst Rechnungsnummer oder Titel. */
function dateiname(beleg: BelegDetail): string {
  return original(beleg)?.pfad.split('/').pop() ?? beleg.rechnungsnummer ?? beleg.titel
}

/** Grund, warum der Beleg nicht aufgeteilt werden kann, oder `null`. */
export function aufteilenGrund(beleg: BelegDetail): string | null {
  if (!istPdf(original(beleg))) return 'Nur PDF-Belege können aufgeteilt werden.'
  if (beleg.seitenanzahl < 2) return 'Der Beleg hat nur eine Seite und kann nicht aufgeteilt werden.'
  if (beleg.teilbelege.length > 0) return 'Der Beleg wurde bereits aufgeteilt.'
  return null
}

/**
 * Dialog „Beleg aufteilen“: Miniaturen aller Seiten, zwischen je zwei Seiten ein Schnitt-Button.
 * Absenden legt je Teil einen neuen Import-Vorgang an; danach geht es zu „Hochladen & Import“.
 */
export function AufteilenDialog({
  beleg,
  onSchliessen,
  onAufgeteilt,
}: {
  beleg: BelegDetail
  onSchliessen: () => void
  onAufgeteilt: () => void
}) {
  const toast = useToast()
  const aufteilen = useAufteilen()
  const [schnitte, setSchnitte] = useState<number[]>([])
  const [fehler, setFehler] = useState<string | null>(null)
  const grund = aufteilenGrund(beleg)
  const n = beleg.seitenanzahl
  const teile = teileAusSchnitten(n, schnitte)
  const teilVonSeite = (seite: number) => teile.findIndex((t) => t.seiten.includes(seite))

  function umschalten(nach: number) {
    setFehler(null)
    setSchnitte((alt) => (alt.includes(nach) ? alt.filter((s) => s !== nach) : [...alt, nach]))
  }

  async function absenden() {
    setFehler(null)
    try {
      await aufteilen.mutateAsync({ id: beleg.id, teile })
      toast.erfolg(`Beleg wird in ${teile.length} Belege aufgeteilt`)
      onAufgeteilt()
    } catch (e) {
      setFehler(istApiFehler(e) ? e.titel : 'Der Beleg konnte nicht aufgeteilt werden.')
    }
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent className="max-w-[760px]">
        <DialogTitle className="pr-8">Beleg aufteilen</DialogTitle>
        {grund ? (
          <DialogDescription>{grund}</DialogDescription>
        ) : (
          <>
            <DialogDescription>
              {dateiname(beleg)} · {n} Seiten. Klicke zwischen zwei Seiten, um einen Schnitt zu setzen.
            </DialogDescription>
            <div className="flex items-stretch gap-2 overflow-x-auto pt-3 pb-1">
              {Array.from({ length: n }, (_, i) => i + 1).map((seite) => {
                const teil = teilVonSeite(seite)
                const ton = toene[teil % toene.length]
                const erste = teile[teil].seiten[0] === seite
                const geschnitten = schnitte.includes(seite)
                return (
                  <Fragment key={seite}>
                    <figure
                      className="relative m-0 flex w-[120px] flex-none flex-col gap-1 rounded-md border-2 bg-karte p-1.5"
                      style={{ borderColor: ton.rand }}
                    >
                      {erste && (
                        <span className={`chip ${ton.chip} absolute -top-[11px] left-1.5`}>{teilBuchstabe(teil)}</span>
                      )}
                      <img
                        src={`${belegVorschauUrl(beleg.id, seite)}&breite=200`}
                        alt={`Seite ${seite}`}
                        loading="lazy"
                        className="aspect-[1/1.414] w-full object-contain"
                      />
                      <figcaption className="text-center text-[11px] text-gedaempft">{seite}</figcaption>
                    </figure>
                    {seite < n && (
                      <button
                        type="button"
                        aria-pressed={geschnitten}
                        aria-label={`Schnitt nach Seite ${seite}`}
                        onClick={() => umschalten(seite)}
                        className={`flex w-7 flex-none cursor-pointer flex-col items-center gap-1 rounded-md border-0 bg-transparent py-1 hover:bg-hover ${
                          geschnitten ? 'text-rot-text' : 'text-gedaempft'
                        }`}
                      >
                        <Icon name="content_cut" />
                        <span
                          aria-hidden="true"
                          className="w-0 flex-1"
                          style={{
                            borderLeft: geschnitten
                              ? '2px dashed var(--color-rot-text)'
                              : '2px dashed var(--color-rand)',
                          }}
                        />
                      </button>
                    )}
                  </Fragment>
                )
              })}
            </div>
            <ul aria-label="Teile" className="m-0 flex list-none flex-col gap-1.5 p-0 text-[13px]">
              {teile.map((teil, i) => (
                <li key={teil.seiten[0]}>
                  <span className={`chip ${toene[i % toene.length].chip}`}>{teilBuchstabe(i)}</span>{' '}
                  {seitenText(teil.seiten)} → neuer Beleg
                </li>
              ))}
            </ul>
            <div className="flex gap-2.5 rounded-[10px] bg-grau-flaeche px-3.5 py-3 text-[13px] text-gedaempft-stark">
              <Icon name="lock" />
              <span>
                Das ungeteilte Original bleibt unverändert und wird mit beiden neuen Belegen verknüpft. Die Teile laufen
                erneut durch die Pipeline.
              </span>
            </div>
            {fehler && (
              <p role="alert" className="m-0 text-sm text-rot-text">
                {fehler}
              </p>
            )}
          </>
        )}
        <DialogFooter>
          {grund ? (
            <button type="button" className="btn" onClick={onSchliessen}>
              Schließen
            </button>
          ) : (
            <>
              <button type="button" className="btn" onClick={onSchliessen}>
                Abbrechen
              </button>
              <button
                type="button"
                className="btn btn-primaer"
                disabled={schnitte.length === 0 || aufteilen.isPending}
                aria-busy={aufteilen.isPending}
                onClick={() => void absenden()}
              >
                In {teile.length} Belege aufteilen
              </button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
