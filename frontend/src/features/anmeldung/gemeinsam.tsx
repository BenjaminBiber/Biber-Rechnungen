import { useId, type ComponentProps, type ReactNode } from 'react'
import { istApiFehler } from '@/api/client'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'

/** Schritte der Anmeldeseite (Suchparameter `schritt`). */
export const schritte = ['anmelden', 'totp', 'registrieren', 'freigabe', 'ersteinrichtung'] as const
export type Schritt = (typeof schritte)[number]

/** Fehler, mit denen der Server auf die Anmeldeseite zurückleitet (Suchparameter `fehler`). */
export const anmeldeFehler = ['google-unbekannt', 'gesperrt'] as const
export type AnmeldeFehler = (typeof anmeldeFehler)[number]

export const MINDEST_PASSWORT = 12

const emailMuster = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export function pruefeEmail(email: string): string | undefined {
  return emailMuster.test(email.trim()) ? undefined : 'Bitte eine gültige E-Mail-Adresse eingeben.'
}

export function pruefePasswort(passwort: string): string | undefined {
  return passwort.length >= MINDEST_PASSWORT ? undefined : `Mindestens ${MINDEST_PASSWORT} Zeichen.`
}

export function pruefeName(name: string): string | undefined {
  return name.trim() ? undefined : 'Bitte einen Namen eingeben.'
}

/** Erste Fehlermeldung des Servers (`ProblemDetails.errors`) zu einem Feld; Groß-/Kleinschreibung egal. */
export function serverFehler(fehler: unknown, feld: string): string | undefined {
  if (!istApiFehler(fehler) || !fehler.fehler) return undefined
  const schluessel = Object.keys(fehler.fehler).find((k) => k.toLowerCase() === feld.toLowerCase())
  return schluessel ? fehler.fehler[schluessel]?.[0] : undefined
}

/** Allgemeine Meldung eines Serverfehlers, der keinem Feld zugeordnet ist. */
export function allgemeinerFehler(fehler: unknown): string | undefined {
  if (!fehler) return undefined
  if (istApiFehler(fehler)) return fehler.fehler && Object.keys(fehler.fehler).length > 0 ? undefined : fehler.titel
  return 'Das hat nicht geklappt. Bitte erneut versuchen.'
}

/** Eingabefeld mit `<label>` und per `aria-describedby` verbundener Fehlermeldung. */
export function Feld({
  label,
  fehler,
  zusatz,
  unterhalb,
  ...props
}: ComponentProps<'input'> & { label: string; fehler?: string; zusatz?: ReactNode; unterhalb?: ReactNode }) {
  const id = useId()
  const fehlerId = `${id}-fehler`
  return (
    <div>
      <div className="flex justify-between">
        <label htmlFor={id} className="label mb-1.5 block">
          {label}
        </label>
        {zusatz}
      </div>
      <Input
        id={id}
        className="h-11 w-full text-[15px]"
        aria-invalid={fehler ? true : undefined}
        aria-describedby={fehler ? fehlerId : undefined}
        {...props}
      />
      {unterhalb}
      {fehler && (
        <p id={fehlerId} className="mt-1 text-[13px] text-rot-text">
          {fehler}
        </p>
      )}
    </div>
  )
}

/** Checkbox mit Beschriftung („Angemeldet bleiben“). */
export function Ankreuzfeld({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (wert: boolean) => void
}) {
  const id = useId()
  return (
    <div className="flex items-center gap-2 text-sm text-gedaempft-stark">
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="m-0 size-[18px] accent-akzent"
      />
      <label htmlFor={id}>{label}</label>
    </div>
  )
}

/** Überschrift eines Schritts mit Untertitel. */
export function SchrittTitel({ titel, text }: { titel: string; text?: string }) {
  return (
    <div>
      <h1 className="m-0 text-2xl font-semibold">{titel}</h1>
      {text && <p className="mt-1.5 mb-0 text-sm text-gedaempft">{text}</p>}
    </div>
  )
}

/** Hinweis- oder Fehlerkasten; Fehler werden vorgelesen. */
export function Meldung({ art = 'fehler', children }: { art?: 'fehler' | 'info'; children: ReactNode }) {
  return (
    <div
      role={art === 'fehler' ? 'alert' : 'status'}
      className={cn(
        'rounded-lg px-3 py-2.5 text-sm',
        art === 'fehler' ? 'bg-rot-flaeche text-rot-text' : 'bg-blau-flaeche text-blau-text',
      )}
    >
      {children}
    </div>
  )
}

/** Textschaltfläche im Akzent (z. B. „Registrieren“ unter dem Formular). */
export function TextKnopf({ className, ...props }: ComponentProps<'button'>) {
  return (
    <button
      type="button"
      className={cn(
        'cursor-pointer border-0 bg-transparent p-0 text-sm font-medium text-akzent-text hover:underline',
        className,
      )}
      {...props}
    />
  )
}
