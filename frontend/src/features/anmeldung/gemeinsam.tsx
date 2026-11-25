import { useNavigate } from '@tanstack/react-router'
import { useId, type ComponentProps, type ReactNode } from 'react'
import { googleStartUrl, useAuthStatus } from '@/api/auth'
import { istApiFehler } from '@/api/client'
import { Icon } from '@/components/Icon'
import { usePasswortAnzeigen } from '@/components/PasswortAnzeigen'
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

/**
 * Eingabefeld der Anmeldung (Login.dc.html, `.field`): Icon links, Beschriftung als Platzhalter und
 * unsichtbares `<label>`, Fehlermeldung per `aria-describedby`.
 */
export function Feld({
  label,
  icon,
  fehler,
  unterhalb,
  className,
  type,
  ...props
}: ComponentProps<'input'> & { label: string; icon: string; fehler?: string; unterhalb?: ReactNode }) {
  const id = useId()
  const passwort = usePasswortAnzeigen(type, label)
  const fehlerId = `${id}-fehler`
  return (
    <div>
      <div
        className={cn(
          'flex h-12 items-center gap-2.5 rounded-[10px] border bg-karte pr-1.5 pl-3.5 text-gedaempft focus-within:border-akzent focus-within:shadow-[0_0_0_3px_var(--color-blau-flaeche)]',
          fehler ? 'border-rot-text' : 'border-eingabe-rand',
        )}
      >
        <Icon name={icon} />
        <label htmlFor={id} className="sr-only">
          {label}
        </label>
        <input
          id={id}
          placeholder={label}
          className={cn(
            'h-full min-w-0 flex-1 border-0 bg-transparent font-sans text-[15px] text-text outline-none placeholder:text-gedaempft',
            className,
          )}
          aria-invalid={fehler ? true : undefined}
          aria-describedby={fehler ? fehlerId : undefined}
          type={passwort.type}
          {...props}
        />
        {passwort.knopf}
      </div>
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

/**
 * Reiter „Anmelden“ / „Konto erstellen“ über dem Formular (Login.dc.html). Der zweite Reiter erscheint nur,
 * wenn die Registrierung offen ist; die Reiter wechseln den Suchparameter `schritt`.
 */
export function AnmeldeReiter({ aktiv }: { aktiv: 'anmelden' | 'registrieren' }) {
  const navigate = useNavigate()
  const { data: status } = useAuthStatus()
  const reiter: Array<{ schritt: 'anmelden' | 'registrieren'; label: string }> = [
    { schritt: 'anmelden', label: 'Anmelden' },
  ]
  if (status?.registrierungOffen || aktiv === 'registrieren')
    reiter.push({ schritt: 'registrieren', label: 'Konto erstellen' })

  return (
    <div role="tablist" aria-label="Anmelden oder Konto erstellen" className="flex">
      {reiter.map((r) => {
        const gewaehlt = r.schritt === aktiv
        return (
          <button
            key={r.schritt}
            type="button"
            role="tab"
            aria-selected={gewaehlt}
            onClick={() =>
              void navigate({ to: '/anmelden', search: r.schritt === 'anmelden' ? {} : { schritt: 'registrieren' } })
            }
            className={cn(
              'h-12 flex-1 cursor-pointer border-0 border-b-2 border-solid bg-transparent font-sans text-[15px] font-medium',
              gewaehlt ? 'border-akzent-text text-akzent-text' : 'border-rand text-gedaempft hover:text-text',
            )}
          >
            {r.label}
          </button>
        )
      })}
    </div>
  )
}

/** Offizielles mehrfarbiges „G“ von Google (Vorgabe der Google-Branding-Richtlinien für Anmeldeknöpfe). */
export function GoogleZeichen({ className }: { className?: string }) {
  return (
    <svg width="18" height="18" viewBox="0 0 48 48" aria-hidden="true" className={cn('flex-none', className)}>
      <path
        fill="#EA4335"
        d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
      />
      <path
        fill="#4285F4"
        d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
      />
      <path
        fill="#FBBC05"
        d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
      />
      <path
        fill="#34A853"
        d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
      />
    </svg>
  )
}

/** „ODER“ zwischen zwei Linien. */
export function Trenner() {
  return (
    <div className="flex items-center gap-3 text-xs tracking-[0.08em] text-gedaempft">
      <span className="h-px flex-1 bg-rand" />
      ODER
      <span className="h-px flex-1 bg-rand" />
    </div>
  )
}

/** Maße der großen Knöpfe auf der Anmeldeseite (48 px hoch, Radius 10 px). */
export const grosserKnopf = 'h-12 w-full rounded-[10px] text-[15px]'

/** Google-Knopf („Mit Google anmelden“ / „… registrieren“); nur zeigen, wenn Google-Anmeldung aktiv ist. */
export function GoogleKnopf({ children }: { children: ReactNode }) {
  return (
    <a href={googleStartUrl()} className={cn('btn text-text no-underline', grosserKnopf)}>
      <GoogleZeichen />
      {children}
    </a>
  )
}

/** Textschaltfläche im Akzent (z. B. „Passwort vergessen?“). */
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
