/*
 * Zeiträume für Filter (Basis Rechnungsdatum). Grenzen sind reine Datumswerte „JJJJ-MM-TT“, beide inklusive.
 */

export type ZeitraumVorgabeId =
  | 'dieserMonat'
  | 'letzterMonat'
  | 'diesesQuartal'
  | 'letztesQuartal'
  | 'diesesJahr'
  | 'vorjahr'

export const zeitraumVorgaben: { id: ZeitraumVorgabeId; text: string }[] = [
  { id: 'dieserMonat', text: 'Dieser Monat' },
  { id: 'letzterMonat', text: 'Letzter Monat' },
  { id: 'diesesQuartal', text: 'Dieses Quartal' },
  { id: 'letztesQuartal', text: 'Letztes Quartal' },
  { id: 'diesesJahr', text: 'Dieses Jahr' },
  { id: 'vorjahr', text: 'Vorjahr' },
]

const zwei = (n: number) => String(n).padStart(2, '0')

/** Datum aus Jahr und Monat (0-basiert, darf über- oder unterlaufen) und Tag als „JJJJ-MM-TT“. */
function iso(jahr: number, monat: number, tag: number): string {
  const d = new Date(Date.UTC(jahr, monat, tag))
  return `${d.getUTCFullYear()}-${zwei(d.getUTCMonth() + 1)}-${zwei(d.getUTCDate())}`
}

/** Erster bis letzter Tag der `anzahl` Monate ab `monat` (0-basiert, darf negativ sein). */
function monate(jahr: number, monat: number, anzahl: number): { von: string; bis: string } {
  return { von: iso(jahr, monat, 1), bis: iso(jahr, monat + anzahl, 0) }
}

/** Grenzen einer Vorgabe relativ zu `heute` (lokales Datum). */
export function zeitraumVorgabe(id: ZeitraumVorgabeId, heute: Date): { von: string; bis: string } {
  const jahr = heute.getFullYear()
  const monat = heute.getMonth()
  const quartalsStart = monat - (monat % 3)
  switch (id) {
    case 'dieserMonat':
      return monate(jahr, monat, 1)
    case 'letzterMonat':
      return monate(jahr, monat - 1, 1)
    case 'diesesQuartal':
      return monate(jahr, quartalsStart, 3)
    case 'letztesQuartal':
      return monate(jahr, quartalsStart - 3, 3)
    case 'diesesJahr':
      return monate(jahr, 0, 12)
    case 'vorjahr':
      return monate(jahr - 1, 0, 12)
  }
}

/** „TT.MM.JJJJ“ → „JJJJ-MM-TT“; `null` bei ungültigem Format oder nicht existierendem Datum. */
export function datumEinlesen(text: string): string | null {
  const treffer = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(text.trim())
  if (!treffer) return null
  const [tag, monat, jahr] = [Number(treffer[1]), Number(treffer[2]), Number(treffer[3])]
  const ergebnis = iso(jahr, monat - 1, tag)
  return ergebnis === `${jahr}-${zwei(monat)}-${zwei(tag)}` ? ergebnis : null
}
