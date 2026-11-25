import { useKorrespondenten } from '@/api/korrespondenten'
import { useKategorien, useTags } from '@/api/tags'
import { formatDatum, formatZahl } from '@/lib/format'
import { zeitraumVorgabe, zeitraumVorgaben, type ZeitraumVorgabeId } from '@/lib/zeitraum'
import { quelleText, zahlstatusText, type BelegeSuche } from './suche'

/** Filter, deren gesetzten Wert die Filterleiste als Kurztext anzeigt. */
export type FilterArt =
  'korrespondent' | 'tags' | 'kategorie' | 'datum' | 'zahlstatus' | 'brutto' | 'netto' | 'herkunft'

const euroKurz = (n: number) => `${formatZahl(n)} €`

/** Gewählte Vorgabe zu einem Zeitraum, sonst `undefined`. */
function vorgabeZu(von: string | undefined, bis: string | undefined, heute: Date): ZeitraumVorgabeId | undefined {
  if (!von || !bis) return undefined
  return zeitraumVorgaben.find((v) => {
    const z = zeitraumVorgabe(v.id, heute)
    return z.von === von && z.bis === bis
  })?.id
}

export type Namen = Record<'korrespondent' | 'tags' | 'kategorie', Map<string, string>>

/** Anzeigenamen zu den IDs von Korrespondenten, Tags und Kategorien. */
export function useNamen(): Namen {
  const korrespondenten = useKorrespondenten()
  const tags = useTags()
  const kategorien = useKategorien()
  const karte = (liste?: { id: string; name: string }[]) => new Map((liste ?? []).map((e) => [e.id, e.name]))
  return { korrespondent: karte(korrespondenten.data), tags: karte(tags.data), kategorie: karte(kategorien.data) }
}

/** Kurztext eines aktiven Filters („Hostwerk Süd GmbH“, „2 gewählt“, „ab 200 €“ …), leer wenn inaktiv. */
export function filterWert(art: FilterArt, suche: BelegeSuche, namen: Namen, heute: Date): string {
  const auswahl = (ids: string[] | undefined, karte: Map<string, string>) => {
    if (!ids?.length) return ''
    return ids.length === 1 ? (karte.get(ids[0]) ?? '1 gewählt') : `${ids.length} gewählt`
  }
  switch (art) {
    case 'korrespondent':
      return auswahl(suche.korrespondenten, namen.korrespondent)
    case 'tags':
      return auswahl(suche.tags, namen.tags)
    case 'kategorie':
      return auswahl(suche.kategorien, namen.kategorie)
    case 'datum': {
      const { von, bis } = suche
      const vorgabe = vorgabeZu(von, bis, heute)
      if (vorgabe) return zeitraumVorgaben.find((v) => v.id === vorgabe)!.text
      if (von && bis) return `${formatDatum(von)} – ${formatDatum(bis)}`
      if (von) return `ab ${formatDatum(von)}`
      return bis ? `bis ${formatDatum(bis)}` : ''
    }
    case 'zahlstatus':
      return suche.zahlstatus ? zahlstatusText[suche.zahlstatus] : ''
    case 'brutto':
    case 'netto': {
      // Der Betragsfilter gilt für Brutto oder Netto, je nach Betragsart (Brutto ist die Vorgabe).
      if ((suche.betragArt ?? 'Brutto') !== (art === 'netto' ? 'Netto' : 'Brutto')) return ''
      const { betragVon: von, betragBis: bis } = suche
      if (von !== undefined && bis !== undefined) return `${euroKurz(von)} – ${euroKurz(bis)}`
      if (von !== undefined) return `ab ${euroKurz(von)}`
      return bis !== undefined ? `bis ${euroKurz(bis)}` : ''
    }
    case 'herkunft':
      return (suche.quellen ?? []).map((q) => quelleText[q]).join(', ')
  }
}
