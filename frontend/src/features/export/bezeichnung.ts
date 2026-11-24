import { formatDatum } from '@/lib/format'
import { zeitraumVorgabe, type ZeitraumVorgabeId } from '@/lib/zeitraum'

const MONATE = [
  'Januar',
  'Februar',
  'März',
  'April',
  'Mai',
  'Juni',
  'Juli',
  'August',
  'September',
  'Oktober',
  'November',
  'Dezember',
]

const zwei = (n: number) => String(n).padStart(2, '0')

/** Lokales Datum als „JJJJ-MM-TT“. */
export function isoDatum(datum: Date): string {
  return `${datum.getFullYear()}-${zwei(datum.getMonth() + 1)}-${zwei(datum.getDate())}`
}

/** Letzter Tag des Monats `monat` (1-basiert) als „JJJJ-MM-TT“. */
function monatsEnde(jahr: number, monat: number): string {
  return `${jahr}-${zwei(monat)}-${zwei(new Date(Date.UTC(jahr, monat, 0)).getUTCDate())}`
}

/** Name eines Monats (1–12), z. B. „Oktober“. */
export function monatsName(monat: number): string {
  return MONATE[monat - 1]
}

/**
 * Bezeichnung eines Export-Zeitraums: „Q3 2026“, „Oktober 2026“, „Jahr 2025“, „Q4 2026 (bis heute)“,
 * „Jahr 2026 (bis heute)“, sonst „01.07.2026 – 15.08.2026“.
 */
export function exportBezeichnung(von: string, bis: string, heute: Date): string {
  const [jahr, monat, tag] = von.split('-').map(Number)
  const bisJahr = Number(bis.slice(0, 4))
  const bisHeute = bis === isoDatum(heute)
  if (tag === 1 && jahr === bisJahr) {
    const bisMonat = Number(bis.slice(5, 7))
    if (monat === 1 && bis === `${jahr}-12-31`) return `Jahr ${jahr}`
    const quartalsBeginn = (monat - 1) % 3 === 0
    const quartal = `Q${(monat + 2) / 3} ${jahr}`
    if (quartalsBeginn && bis === monatsEnde(jahr, monat + 2)) return quartal
    if (bis === monatsEnde(jahr, monat)) return `${monatsName(monat)} ${jahr}`
    if (quartalsBeginn && bisHeute && bisMonat <= monat + 2) return `${quartal} (bis heute)`
    if (monat === 1 && bisHeute) return `Jahr ${jahr} (bis heute)`
  }
  return `${formatDatum(von)} – ${formatDatum(bis)}`
}

export type Schnellwahl = { id: ZeitraumVorgabeId; text: string; von: string; bis: string }

/**
 * Schnellwahl des Exports: Vormonat, laufender Monat, letztes und laufendes Quartal, laufendes Jahr, Vorjahr.
 * Laufendes Quartal und Jahr enden heute.
 */
export function schnellwahl(heute: Date): Schnellwahl[] {
  const jahr = heute.getFullYear()
  const heuteIso = isoDatum(heute)
  const quartal = (z: { von: string }) => `Q${Math.floor(Number(z.von.slice(5, 7)) / 3) + 1} ${z.von.slice(0, 4)}`
  const monat = (z: { von: string }) => monatsName(Number(z.von.slice(5, 7)))
  const eintrag = (id: ZeitraumVorgabeId, text: (z: { von: string; bis: string }) => string, bisHeute = false) => {
    const z = zeitraumVorgabe(id, heute)
    const grenzen = bisHeute ? { von: z.von, bis: heuteIso } : z
    return { id, text: text(z), ...grenzen }
  }
  return [
    eintrag('letzterMonat', monat),
    eintrag('dieserMonat', monat),
    eintrag('letztesQuartal', quartal),
    eintrag('diesesQuartal', quartal, true),
    eintrag('diesesJahr', () => `Jahr ${jahr}`, true),
    eintrag('vorjahr', () => 'Vorjahr'),
  ]
}
