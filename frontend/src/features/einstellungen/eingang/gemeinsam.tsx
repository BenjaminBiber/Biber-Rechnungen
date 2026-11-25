import type { UseMutationResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useEinstellungen } from '@/api/einstellungen'
import type { AktionErgebnis, Einstellungen } from '@/api/typen'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { zahlLesen } from '../pruefungen'

/**
 * Wie `Bereich`, aber mit Platz für einen Status-Chip neben der Überschrift
 * (E-Mail-Import, Eingangsordner in Einstellungen.dc.html).
 */
export function BereichMitStatus({
  titel,
  status,
  children,
}: {
  titel: string
  status?: ReactNode
  children: (e: Einstellungen) => ReactNode
}) {
  const { data, isError, error } = useEinstellungen()
  return (
    <div className="flex flex-col gap-[18px]">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <h2 className="m-0 text-lg font-semibold text-text">{titel}</h2>
        {status}
      </div>
      {data ? (
        children(data)
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : (
        <p className="gedaempft m-0 text-sm">Lädt …</p>
      )}
    </div>
  )
}

/** Raster mit zwei Spalten (`.g2` im Design). */
export function Raster({ children }: { children: ReactNode }) {
  return <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">{children}</div>
}

/** Schreibgeschütztes Feld, z. B. Werte aus der `.env`. */
export function NurLesenFeld({ id, label, wert, mono }: { id: string; label: string; wert: string; mono?: boolean }) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <input id={id} className={`eingabe${mono ? ' mono' : ''}`} readOnly value={wert} />
    </div>
  )
}

/** Ganzzahlfeld mit Einheit dahinter und Fehlermeldung darunter. */
export function ZahlFeld({
  id,
  label,
  wert,
  einheit,
  hinweis,
  fehler,
  onAendern,
}: {
  id: string
  label: string
  wert: string
  einheit?: string
  hinweis?: string
  fehler?: string
  onAendern: (wert: string) => void
}) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <span className="flex items-center gap-1.5">
        <input
          id={id}
          className="eingabe num"
          inputMode="numeric"
          aria-invalid={!!fehler}
          value={wert}
          onChange={(ev) => onAendern(ev.target.value)}
        />
        {einheit && <span className="gedaempft text-[13px]">{einheit}</span>}
      </span>
      {hinweis && <span className="hinweis">{hinweis}</span>}
      {fehler && (
        <span role="alert" className="block text-[13px] text-gefahr">
          {fehler}
        </span>
      )}
    </div>
  )
}

/** Ganze Zahl im Bereich `min`–`max`; sonst `undefined`. */
export function ganzzahl(text: string, min: number, max = Number.MAX_SAFE_INTEGER): number | undefined {
  const zahl = zahlLesen(text)
  return zahl !== undefined && Number.isInteger(zahl) && zahl >= min && zahl <= max ? zahl : undefined
}

/** Knopf für eine Aktion mit `AktionErgebnis`: Erfolg als Toast, sonst stehender Fehler-Toast. */
export function AktionsKnopf({
  aktion,
  disabled,
  danach,
  children,
}: {
  aktion: UseMutationResult<AktionErgebnis, Error, void>
  disabled?: boolean
  danach?: () => void
  children: ReactNode
}) {
  const toast = useToast()
  return (
    <Button
      type="button"
      disabled={disabled || aktion.isPending}
      laedt={aktion.isPending}
      onClick={() =>
        aktion.mutateAsync().then(
          (e) => {
            if (e.erfolg) toast.erfolg(e.meldung)
            else toast.fehler(e.meldung)
            danach?.()
          },
          (f: Error) => toast.fehler(f.message),
        )
      }
    >
      {children}
    </Button>
  )
}

/** „vor 3 Min.“ relativ zu jetzt. */
export function vorZeit(iso: string, jetzt = Date.now()): string {
  const minuten = Math.max(0, Math.floor((jetzt - new Date(iso).getTime()) / 60000))
  if (minuten < 1) return 'gerade eben'
  if (minuten < 60) return `vor ${minuten} Min.`
  const stunden = Math.floor(minuten / 60)
  if (stunden < 24) return `vor ${stunden} Std.`
  const tage = Math.floor(stunden / 24)
  return `vor ${tage} ${tage === 1 ? 'Tag' : 'Tagen'}`
}
