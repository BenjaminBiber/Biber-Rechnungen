import type { ApiTokenErstellt } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { mcpBefehl } from './mcpBefehl'

/**
 * Dialog „API-Token erstellt“ (Dialoge.dc.html): zeigt den Klartext genau einmal.
 * Der Klartext lebt nur in `erstellt`; nach `onSchliessen` verwirft der Aufrufer ihn.
 */
export function TokenErstelltDialog({
  erstellt,
  oeffentlicheUrl,
  onSchliessen,
}: {
  erstellt: ApiTokenErstellt
  oeffentlicheUrl: string | null | undefined
  onSchliessen: () => void
}) {
  const toast = useToast()
  const befehl = mcpBefehl(oeffentlicheUrl, erstellt.klartext)
  const kopieren = (text: string) =>
    void navigator.clipboard.writeText(text).then(
      () => toast.erfolg('Kopiert'),
      () => toast.fehler('Kopieren nicht möglich'),
    )

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Token „{erstellt.token.name}“ erstellt</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-3 text-sm">
          <div className="flex gap-1.5">
            {erstellt.token.scopes.map((s) => (
              <Chip key={s} ton="blau">
                {s}
              </Chip>
            ))}
          </div>
          <DialogDescription asChild>
            <div className="m-0 flex items-start gap-2.5 rounded-[10px] bg-orange-flaeche p-3 text-orange-text">
              <Icon name="visibility" />
              <span>
                <strong>Nur jetzt sichtbar.</strong> Gespeichert wird nur ein Hash – kopiere den Token sofort.
              </span>
            </div>
          </DialogDescription>
          <div className="flex gap-2">
            <input className="eingabe mono bg-flaeche" readOnly aria-label="Token" value={erstellt.klartext} />
            <Button type="button" onClick={() => kopieren(erstellt.klartext)}>
              <Icon name="content_copy" />
              Kopieren
            </Button>
          </div>
          <div className="flex items-start gap-2">
            <pre
              aria-label="Befehl mit Token"
              className="mono m-0 flex-1 rounded-lg bg-flaeche p-3 text-xs whitespace-pre-wrap break-all"
            >
              {befehl}
            </pre>
            <Button type="button" aria-label="Befehl kopieren" onClick={() => kopieren(befehl)}>
              <Icon name="content_copy" />
              Kopieren
            </Button>
          </div>
        </div>
        <DialogFooter>
          <Button type="button" variant="primaer" onClick={onSchliessen}>
            Fertig
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
