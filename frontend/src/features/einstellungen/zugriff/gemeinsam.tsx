import type { ReactNode } from 'react'
import type { Rolle } from '@/api/typen'
import { usePasswortAnzeigen } from '@/components/PasswortAnzeigen'
import { Button } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { passwortStaerke } from '@/features/anmeldung/RegistrierenFormular'
import { cn } from '@/lib/utils'

export const MINDEST_LAENGE = 12

/** Rollen in der Reihenfolge des Designs (Benutzer zuerst). */
export const rollen: { wert: Rolle; text: string }[] = [
  { wert: 'Benutzer', text: 'Benutzer' },
  { wert: 'NurLesen', text: 'Nur-Lesen' },
  { wert: 'Admin', text: 'Admin' },
]

export const rollenText = (rolle?: Rolle | null) => rollen.find((r) => r.wert === rolle)?.text ?? '—'

/** Rückfrage vor einer folgenreichen Aktion. */
export function Rueckfrage({
  titel,
  text,
  bestaetigen,
  gefaehrlich,
  onBestaetigen,
  onAbbrechen,
}: {
  titel: string
  text: ReactNode
  bestaetigen: string
  gefaehrlich?: boolean
  onBestaetigen: () => void
  onAbbrechen: () => void
}) {
  return (
    <AlertDialog open onOpenChange={(offen) => !offen && onAbbrechen()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{titel}</AlertDialogTitle>
        </AlertDialogHeader>
        <AlertDialogDescription className="m-0 text-sm">{text}</AlertDialogDescription>
        <AlertDialogFooter>
          <Button type="button" onClick={onAbbrechen}>
            Abbrechen
          </Button>
          <Button type="button" variant={gefaehrlich ? 'gefaehrlich' : 'primaer'} onClick={onBestaetigen}>
            {bestaetigen}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

const stufen = [
  { text: `Zu kurz – mindestens ${MINDEST_LAENGE} Zeichen`, farbe: 'bg-rot-text', schrift: 'text-rot-text' },
  { text: 'Schwaches Passwort', farbe: 'bg-rot-text', schrift: 'text-rot-text' },
  { text: 'Mittleres Passwort', farbe: 'bg-orange-text', schrift: 'text-orange-text' },
  { text: 'Starkes Passwort', farbe: 'bg-gruen-text', schrift: 'text-gruen-text' },
  { text: 'Sehr starkes Passwort', farbe: 'bg-gruen-text', schrift: 'text-gruen-text' },
]

/** Stärke-Anzeige wie bei der Registrierung. */
export function StaerkeAnzeige({ passwort }: { passwort: string }) {
  if (!passwort) return null
  const staerke = passwortStaerke(passwort)
  const stufe = stufen[staerke]!
  return (
    <>
      <div className="mt-2 flex gap-1" aria-hidden="true">
        {[1, 2, 3, 4].map((n) => (
          <span key={n} className={cn('h-1 flex-1 rounded-sm', n <= staerke ? stufe.farbe : 'bg-rand')} />
        ))}
      </div>
      <div className={cn('mt-1 text-xs', stufe.schrift)} aria-live="polite">
        {stufe.text}
      </div>
    </>
  )
}

/** Beschriftetes Eingabefeld mit Fehlertext. */
export function Feld({
  id,
  label,
  fehler,
  unterhalb,
  type,
  ...props
}: {
  id: string
  label: string
  fehler?: string
  unterhalb?: ReactNode
} & React.InputHTMLAttributes<HTMLInputElement>) {
  const passwort = usePasswortAnzeigen(type, label)
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      {passwort.knopf ? (
        <div className="relative">
          <input id={id} className="eingabe pr-10" aria-invalid={!!fehler} type={passwort.type} {...props} />
          <span className="absolute inset-y-0 right-px flex items-center [&>button]:size-[30px]">{passwort.knopf}</span>
        </div>
      ) : (
        <input id={id} className="eingabe" aria-invalid={!!fehler} type={type} {...props} />
      )}
      {unterhalb}
      {fehler && (
        <span role="alert" className="mt-1 block text-[13px] text-gefahr">
          {fehler}
        </span>
      )}
    </div>
  )
}
