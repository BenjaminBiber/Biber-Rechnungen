import type { KorrespondentAendern, KorrespondentDetail, LogoQuelle } from '@/api/typen'

export type KorrespondentDetailSuche = { jahr?: number; logoAus?: string }

/** Suchparameter `jahr` und `logoAus` (Beleg-ID für den Logo-Ausschnitt). */
export function detailSuche(suche: Record<string, unknown>): KorrespondentDetailSuche {
  const jahr = Number(suche.jahr)
  const logoAus = typeof suche.logoAus === 'string' && suche.logoAus ? suche.logoAus : undefined
  return {
    ...(Number.isInteger(jahr) && jahr > 1900 ? { jahr } : {}),
    ...(logoAus ? { logoAus } : {}),
  }
}

/** Vollständige Änderung aus dem Detail, damit `PUT` nur die gewünschten Felder ändert. */
export function alsAenderung(d: KorrespondentDetail): KorrespondentAendern {
  return {
    name: d.name,
    aliase: d.aliase,
    anschrift: d.anschrift ?? null,
    ustIdNr: d.ustIdNr ?? null,
    steuernummer: d.steuernummer ?? null,
    bankverbindungen: d.bankverbindungen,
    website: d.website ?? null,
    email: d.email ?? null,
    istLieferant: d.istLieferant,
    istKunde: d.istKunde,
    defaultKategorieId: d.defaultKategorie?.id ?? null,
    matching: d.matching,
  }
}

/** IBAN in Vierergruppen, z. B. „DE12 6805 0101 …“. */
export function ibanFormatiert(iban: string): string {
  return iban
    .replace(/\s+/g, '')
    .toUpperCase()
    .replace(/(.{4})(?=.)/g, '$1 ')
}

const logoQuellen: Record<LogoQuelle, string> = {
  Keine: 'Initialen',
  Upload: 'Hochgeladene Datei',
  Pdf: 'Ausschnitt aus Seite 1',
  Favicon: 'Favicon der Website',
}

/** „Ausschnitt aus Seite 1 (Upload › PDF › Favicon)“. */
export function logoQuelleText(quelle: LogoQuelle): string {
  return `${logoQuellen[quelle]} (Upload › PDF › Favicon)`
}

/** Link-Ziel einer Website ohne Schema. */
export function websiteHref(website: string): string {
  return /^https?:\/\//i.test(website) ? website : `https://${website}`
}
