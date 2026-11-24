import type { ArtikelAbfrage, PreisaenderungFilter } from '@/api/typen'

/** Suchparameter der Artikelseite (`/artikel?reiter=…`). */
export type ArtikelSuche = {
  reiter?: 'artikel' | 'stammartikel'
  suche?: string
  korrespondentId?: string
  preisaenderung?: PreisaenderungFilter
  ohneStammartikel?: boolean
  seite?: number
}

export const ARTIKEL_SEITENGROESSE = 25

function text(wert: unknown): string | undefined {
  return typeof wert === 'string' && wert.trim() ? wert : undefined
}

export function sucheValidieren(roh: Record<string, unknown>): ArtikelSuche {
  const seite = Number(roh.seite)
  return {
    reiter: roh.reiter === 'stammartikel' ? 'stammartikel' : undefined,
    suche: text(roh.suche),
    korrespondentId: text(roh.korrespondentId),
    preisaenderung:
      roh.preisaenderung === 'Gestiegen' || roh.preisaenderung === 'Gefallen' ? roh.preisaenderung : undefined,
    ohneStammartikel: roh.ohneStammartikel === true || roh.ohneStammartikel === 'true' ? true : undefined,
    seite: Number.isInteger(seite) && seite > 1 ? seite : undefined,
  }
}

/** Entfernt leere Werte, damit die URL kurz bleibt. */
export function ohneLeere(suche: ArtikelSuche): ArtikelSuche {
  return Object.fromEntries(Object.entries(suche).filter(([, w]) => w !== undefined && w !== '')) as ArtikelSuche
}

export function zuAbfrage(suche: ArtikelSuche): ArtikelAbfrage {
  return ohneLeere({
    suche: suche.suche,
    korrespondentId: suche.korrespondentId,
    preisaenderung: suche.preisaenderung,
    ohneStammartikel: suche.ohneStammartikel,
    seite: suche.seite,
    seitengroesse: ARTIKEL_SEITENGROESSE,
  } as ArtikelSuche) as ArtikelAbfrage
}

/** Kurzname eines Händlers für knappe Angaben: „Bürowelt Krämer KG“ → „Bürowelt“, „Druckerei Weller“ → „Weller“. */
export function kurzname(name: string): string {
  const gattung = ['Druckerei', 'Autohaus', 'Bäckerei', 'Apotheke', 'Firma']
  const woerter = name.split(/\s+/).filter((w) => !/^(GmbH|KG|AG|OHG|UG|e\.K\.|SE|mbH|&|Co\.?)$/.test(w))
  if (woerter.length > 1 && gattung.includes(woerter[0])) return woerter[1]
  return woerter[0] ?? name
}

/** Änderung in Prozent als Chip-Text und -Ton: „+8,3 %“ orange, „−12,5 %“ grün, „±0 %“ grau. */
export function aenderungDarstellung(prozent: number): {
  text: string
  ton: 'orange' | 'gruen' | 'grau'
  label: string
} {
  const betrag = new Intl.NumberFormat('de-DE', { maximumFractionDigits: 1 }).format(Math.abs(prozent))
  if (prozent > 0) return { text: `+${betrag} %`, ton: 'orange', label: 'gestiegen' }
  if (prozent < 0) return { text: `−${betrag} %`, ton: 'gruen', label: 'gefallen' }
  return { text: '±0 %', ton: 'grau', label: 'unverändert' }
}
