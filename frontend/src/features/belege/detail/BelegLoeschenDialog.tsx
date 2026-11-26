import { useNavigate } from '@tanstack/react-router'
import { useId, useState } from 'react'
import { useBelegLoeschen } from '@/api/aufbewahrung'
import type { BelegDetail } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { formatDatum } from '@/lib/format'

/**
 * „Löschen (falsch zugeordnet) …“ für Admins: umgeht die Löschsperre, aber nur mit dem Grund „falsch zugeordnet“.
 * Der Beleg wird nicht vernichtet, sondern ausgeblendet; das Original zieht in der Ablage nach `_Geloescht/` um.
 * Wiederherstellen unter Einstellungen → Gelöschte Belege.
 */
export function BelegLoeschenDialog({ beleg, onSchliessen }: { beleg: BelegDetail; onSchliessen: () => void }) {
  const grundId = useId()
  const bemerkungId = useId()
  const toast = useToast()
  const navigate = useNavigate()
  const loeschen = useBelegLoeschen()
  const [falschZugeordnet, setFalschZugeordnet] = useState(false)
  const [bemerkung, setBemerkung] = useState('')

  async function absenden() {
    try {
      await loeschen.mutateAsync({ id: beleg.id, grund: 'FalschZugeordnet', bemerkung: bemerkung.trim() || null })
      toast.erfolg('Beleg gelöscht – wiederherstellbar unter Einstellungen → Gelöschte Belege')
      onSchliessen()
      void navigate({ to: '/belege' })
    } catch (f) {
      toast.fehler(f instanceof Error ? f.message : 'Beleg konnte nicht gelöscht werden')
    }
  }

  return (
    <AlertDialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <AlertDialogContent className="max-w-[540px]">
        <div className="flex items-start gap-3">
          <span className="flex size-10 flex-none items-center justify-center rounded-full bg-rot-flaeche text-rot-text">
            <Icon name="delete" />
          </span>
          <AlertDialogTitle className="pt-2">Falsch zugeordneten Beleg löschen?</AlertDialogTitle>
        </div>
        <div className="flex flex-col gap-3 text-sm">
          <AlertDialogDescription className="m-0">
            Der Beleg unterliegt der Löschsperre bis {formatDatum(beleg.loeschsperreBis)}. Als Admin darfst du sie nur
            umgehen, wenn der Beleg nicht in dieses Archiv gehört – etwa weil er versehentlich hochgeladen oder falsch
            zugeordnet wurde.
          </AlertDialogDescription>
          <div className="flex items-start gap-2.5 rounded-[10px] bg-flaeche p-3 text-gedaempft-stark">
            <Icon name="restore_from_trash" />
            <span>
              Nichts wird vernichtet: Der Beleg verschwindet aus Belegen, Auswertungen und Exporten, das Original
              wandert in der Ablage in den Ordner <span className="mono">_Geloescht</span>. Unter Einstellungen →
              Gelöschte Belege kannst du ihn wiederherstellen. Die Löschung wird protokolliert.
            </span>
          </div>
          <label htmlFor={grundId} className="flex items-start gap-2.5">
            <Checkbox
              id={grundId}
              className="mt-0.5"
              checked={falschZugeordnet}
              onCheckedChange={(wert) => setFalschZugeordnet(wert === true)}
            />
            <span>
              <strong>Dieser Beleg ist falsch zugeordnet</strong> und gehört nicht in dieses Archiv.
            </span>
          </label>
          <div>
            <label className="label" htmlFor={bemerkungId}>
              Bemerkung (optional, wird protokolliert)
            </label>
            <textarea
              id={bemerkungId}
              className="eingabe resize-none"
              style={{ minHeight: 64 }}
              maxLength={2000}
              placeholder="z. B. Rechnung eines anderen Unternehmens"
              value={bemerkung}
              onChange={(e) => setBemerkung(e.target.value)}
            />
          </div>
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel>Abbrechen</AlertDialogCancel>
          <Button
            type="button"
            variant="gefaehrlich"
            disabled={!falschZugeordnet || loeschen.isPending}
            laedt={loeschen.isPending}
            onClick={() => void absenden()}
          >
            Beleg löschen
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
