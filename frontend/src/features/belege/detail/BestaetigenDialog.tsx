import { useId, useState } from 'react'
import { useBelegBestaetigen } from '@/api/belege'
import type { BelegDetail } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { fehlgeschlageneBefunde } from './gemeinsam'

/**
 * „Bestätigen“ im Kopf: ohne fehlgeschlagene Befunde sofort, sonst über den Dialog
 * „Beleg mit n offenen Befunden bestätigen?“ mit optionaler Begründung.
 */
export function BestaetigenDialog({ beleg }: { beleg: BelegDetail }) {
  const begruendungId = useId()
  const toast = useToast()
  const bestaetigen = useBelegBestaetigen()
  const [offen, setOffen] = useState(false)
  const [begruendung, setBegruendung] = useState('')
  const befunde = fehlgeschlageneBefunde(beleg)

  async function ausfuehren(mitBegruendung?: string) {
    try {
      await bestaetigen.mutateAsync(
        mitBegruendung === undefined ? { id: beleg.id } : { id: beleg.id, begruendung: mitBegruendung.trim() || null },
      )
      setOffen(false)
      setBegruendung('')
      toast.erfolg('Beleg bestätigt')
    } catch {
      toast.fehler('Beleg konnte nicht bestätigt werden')
    }
  }

  function klick() {
    if (befunde.length === 0) void ausfuehren()
    else setOffen(true)
  }

  return (
    <>
      <button
        type="button"
        className="btn btn-primaer"
        onClick={klick}
        disabled={bestaetigen.isPending}
        aria-busy={bestaetigen.isPending}
      >
        <Icon name="task_alt" />
        Bestätigen
      </button>
      <AlertDialog open={offen} onOpenChange={setOffen}>
        <AlertDialogContent className="max-w-[520px]" aria-describedby={undefined}>
          <div className="flex items-start gap-3">
            <span className="flex size-10 flex-none items-center justify-center rounded-full bg-orange-flaeche text-orange-text">
              <Icon name="report" />
            </span>
            <AlertDialogTitle className="pt-2">
              Beleg mit {befunde.length === 1 ? '1 offenen Befund' : `${befunde.length} offenen Befunden`} bestätigen?
            </AlertDialogTitle>
          </div>
          <div className="flex flex-col gap-3 text-sm">
            <ul className="m-0 pl-5 text-gedaempft-stark">
              {befunde.map((b) => (
                <li key={b.pruefung}>{b.detail}</li>
              ))}
            </ul>
            <span>„Zu prüfen“ wird entfernt. Befunde und deine Bestätigung bleiben in der Historie.</span>
            {befunde.some((b) => b.pruefung === 'Bankverbindung') && (
              <span>
                <strong>Abweichende IBAN:</strong> Mit dem Bestätigen wird sie beim Korrespondenten hinterlegt und gilt
                künftig als bekannt. Bestätige nur, wenn der Lieferant die neue Bankverbindung auf anderem Weg bestätigt
                hat.
              </span>
            )}
            <div>
              <label className="label" htmlFor={begruendungId}>
                Begründung (optional, wird protokolliert)
              </label>
              <textarea
                id={begruendungId}
                className="eingabe resize-none"
                style={{ minHeight: 72 }}
                value={begruendung}
                onChange={(e) => setBegruendung(e.target.value)}
              />
            </div>
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>Zurück zum Beleg</AlertDialogCancel>
            <button
              type="button"
              className="btn btn-primaer"
              disabled={bestaetigen.isPending}
              aria-busy={bestaetigen.isPending}
              onClick={() => void ausfuehren(begruendung)}
            >
              Trotzdem bestätigen
            </button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  )
}
