import { useState } from 'react'
import type { Logo } from '@/api/typen'
import type { ChipTon } from '@/components/Chip'

/** Farbe der Initialen-Fläche: ein Chip-Ton oder der Akzent. */
export type LogoTon = ChipTon | 'akzent'

function farben(ton: LogoTon) {
  return ton === 'akzent'
    ? { background: 'var(--color-akzent-zart)', color: 'var(--color-akzent-text)' }
    : { background: `var(--color-${ton}-flaeche)`, color: `var(--color-${ton}-text)` }
}

/**
 * Logo des Korrespondenten, eingepasst auf weißem Grund (auch transparente und dunkle Logos bleiben lesbar).
 * Ohne Logo – oder wenn das Bild nicht lädt (gelöscht, kein Recht, kaputte Datei) – seine Initialen.
 */
export function KorrespondentLogo({
  logo,
  groesse = 32,
  ton = 'grau',
}: {
  logo: Logo
  groesse?: number
  ton?: LogoTon
}) {
  // Merkt sich die URL, die nicht geladen hat; eine neue URL wird wieder versucht.
  const [kaputteUrl, setKaputteUrl] = useState<string | null>(null)
  const radius = Math.max(4, Math.round(groesse * 0.22))
  const rahmen = { width: groesse, height: groesse, borderRadius: radius }

  if (logo.url && logo.url !== kaputteUrl)
    return (
      <img
        src={logo.url}
        alt=""
        loading="lazy"
        decoding="async"
        draggable={false}
        onError={() => setKaputteUrl(logo.url ?? null)}
        style={{ ...rahmen, padding: groesse >= 40 ? 3 : 1 }}
        className="box-border flex-none border border-rand bg-white object-contain"
      />
    )
  return (
    <span
      aria-hidden="true"
      style={{ ...rahmen, ...farben(ton), fontSize: Math.max(9, Math.round(groesse * 0.36)) }}
      className="flex flex-none items-center justify-center font-bold"
    >
      {logo.initialen}
    </span>
  )
}
