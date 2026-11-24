import { HttpResponse } from 'msw'
import type { PreisDetail, StammartikelAendern, StammartikelListeneintrag, ZugeordneterArtikel } from '@/api/typen'
import { db, neueId } from '../db'
import {
  abfrage,
  keinInhalt,
  jsonKoerper,
  nichtGefunden,
  route,
  seite,
  ungueltig,
  wahr,
  zahl,
  type Route,
} from './gemeinsam'

function pruefe(eingabe: StammartikelAendern): Response | undefined {
  if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
  if (!eingabe.basiseinheit?.trim()) return ungueltig('basiseinheit', 'Basiseinheit ist erforderlich.')
  if (!(eingabe.grundpreisMenge > 0)) return ungueltig('grundpreisMenge', 'Menge muss größer als 0 sein.')
  if (eingabe.artikel.some((a) => !db.artikel.some((x) => x.id === a.artikelId)))
    return ungueltig('artikel', 'Unbekannter Artikel.')
  return undefined
}

/** Ordnet die Artikel dem Stammartikel zu und baut Liste und Detail neu auf. */
function speichere(
  id: string,
  eingabe: StammartikelAendern,
  alt?: StammartikelListeneintrag,
): StammartikelListeneintrag {
  const verweis = { id, name: eingabe.name }
  const ids = eingabe.artikel.map((a) => a.artikelId)
  db.artikel = db.artikel.map((a) => {
    if (ids.includes(a.id)) return { ...a, stammartikel: verweis }
    return a.stammartikel?.id === id ? { ...a, stammartikel: null } : a
  })
  const zugeordnet = db.artikel.filter((a) => ids.includes(a.id))
  const eintrag: StammartikelListeneintrag = {
    verlauf: [],
    guenstigsterPreis: null,
    guenstigsterBei: null,
    aenderung: null,
    ...alt,
    id,
    name: eingabe.name,
    basiseinheit: eingabe.basiseinheit,
    grundpreisMenge: eingabe.grundpreisMenge,
    anzahlArtikel: zugeordnet.length,
    korrespondenten: [...new Set(zugeordnet.map((a) => a.korrespondent.name))],
  }
  db.stammartikel = alt ? db.stammartikel.map((s) => (s.id === id ? eintrag : s)) : [...db.stammartikel, eintrag]
  const artikel: ZugeordneterArtikel[] = eingabe.artikel.map((z) => {
    const a = db.artikel.find((x) => x.id === z.artikelId)!
    return {
      id: a.id,
      name: a.name,
      korrespondent: a.korrespondent,
      einheit: a.einheit,
      umrechnungFaktor: z.umrechnungFaktor,
    }
  })
  const vorher: Pick<PreisDetail, 'serien' | 'kaeufe' | 'kennzahlen'> = db.preisDetails[id] ?? {
    serien: [],
    kaeufe: [],
    kennzahlen: {
      guenstigsterGrundpreis: null,
      guenstigsterBei: null,
      aenderungSeitJahresbeginn: null,
      preisVon: null,
      preisBis: null,
      mengeJahr: 0,
      ausgabenJahrNetto: 0,
      anzahlKaeufe: 0,
    },
  }
  db.preisDetails[id] = {
    serien: vorher.serien,
    kaeufe: vorher.kaeufe,
    kennzahlen: vorher.kennzahlen,
    id,
    name: eingabe.name,
    istStammartikel: true,
    basiseinheit: eingabe.basiseinheit,
    grundpreisMenge: eingabe.grundpreisMenge,
    artikel,
  } satisfies PreisDetail
  return eintrag
}

export const artikelRouten: Route[] = [
  route('get', '/api/artikel', 'Lesen', ({ request }) => {
    const parameter = abfrage(request)
    const suche = parameter.get('suche')?.trim().toLocaleLowerCase('de')
    const korrespondentId = parameter.get('korrespondentId')
    const ohneStammartikel = wahr(parameter, 'ohneStammartikel') ?? false
    const preisaenderung = parameter.get('preisaenderung')
    const treffer = db.artikel.filter((a) => {
      if (suche && !a.name.toLocaleLowerCase('de').includes(suche)) return false
      if (korrespondentId && a.korrespondent.id !== korrespondentId) return false
      if (ohneStammartikel && a.stammartikel) return false
      if (preisaenderung === 'Gestiegen' && !((a.aenderungSeitErstkauf ?? 0) > 0)) return false
      if (preisaenderung === 'Gefallen' && !((a.aenderungSeitErstkauf ?? 0) < 0)) return false
      return true
    })
    return HttpResponse.json(seite(treffer, zahl(parameter, 'seite') ?? 1, zahl(parameter, 'seitengroesse') ?? 25))
  }),

  route('get', '/api/artikel/{id}', 'Lesen', ({ params }) => {
    const detail = db.artikel.some((a) => a.id === params.id) ? db.preisDetails[params.id] : undefined
    return detail ? HttpResponse.json(detail) : nichtGefunden('Artikel')
  }),

  route('get', '/api/stammartikel', 'Lesen', () => HttpResponse.json(db.stammartikel)),

  route('post', '/api/stammartikel', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<StammartikelAendern>(request)
    const fehler = pruefe(eingabe)
    if (fehler) return fehler
    return HttpResponse.json(speichere(neueId(), eingabe), { status: 201 })
  }),

  route('get', '/api/stammartikel/{id}', 'Lesen', ({ params }) => {
    const detail = db.stammartikel.some((s) => s.id === params.id) ? db.preisDetails[params.id] : undefined
    return detail ? HttpResponse.json(detail) : nichtGefunden('Stammartikel')
  }),

  route('put', '/api/stammartikel/{id}', 'Bearbeiten', async ({ request, params }) => {
    const alt = db.stammartikel.find((s) => s.id === params.id)
    if (!alt) return nichtGefunden('Stammartikel')
    const eingabe = await jsonKoerper<StammartikelAendern>(request)
    const fehler = pruefe(eingabe)
    if (fehler) return fehler
    return HttpResponse.json(speichere(alt.id, eingabe, alt))
  }),

  route('delete', '/api/stammartikel/{id}/artikel/{artikelId}', 'Bearbeiten', ({ params }) => {
    const stamm = db.stammartikel.find((s) => s.id === params.id)
    const artikel = db.artikel.find((a) => a.id === params.artikelId && a.stammartikel?.id === params.id)
    if (!stamm || !artikel) return nichtGefunden('Zuordnung')
    db.artikel = db.artikel.map((a) => (a.id === artikel.id ? { ...a, stammartikel: null } : a))
    const rest = db.artikel.filter((a) => a.stammartikel?.id === stamm.id)
    db.stammartikel = db.stammartikel.map((s) =>
      s.id === stamm.id
        ? { ...s, anzahlArtikel: rest.length, korrespondenten: [...new Set(rest.map((a) => a.korrespondent.name))] }
        : s,
    )
    const detail = db.preisDetails[stamm.id]
    if (detail) {
      detail.artikel = detail.artikel.filter((a) => a.id !== artikel.id)
      detail.serien = detail.serien.filter(
        (s) =>
          s.korrespondent.id !== artikel.korrespondent.id ||
          rest.some((a) => a.korrespondent.id === s.korrespondent.id),
      )
    }
    return keinInhalt()
  }),
]
