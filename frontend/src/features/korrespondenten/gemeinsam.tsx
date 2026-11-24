import type { Logo } from '@/api/typen'

export type KorrespondentenTyp = 'Lieferanten' | 'Kunden' | 'Alle'

export const korrespondentenTypen: KorrespondentenTyp[] = ['Lieferanten', 'Kunden', 'Alle']

/** Logo des Korrespondenten oder seine Initialen auf grauer Fläche. */
export function KorrespondentLogo({ logo, groesse = 32 }: { logo: Logo; groesse?: number }) {
  const stil = { width: groesse, height: groesse }
  if (logo.url)
    return (
      <img
        src={logo.url}
        alt=""
        style={stil}
        className="flex-none rounded-[7px] border border-rand bg-karte object-contain"
      />
    )
  return (
    <span
      aria-hidden="true"
      style={stil}
      className="flex flex-none items-center justify-center rounded-[7px] bg-[var(--color-grau-flaeche)] text-xs font-bold text-[var(--color-grau-text)]"
    >
      {logo.initialen}
    </span>
  )
}
