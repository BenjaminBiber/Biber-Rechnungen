import { HttpResponse } from 'msw'
import type {
  AnzahlGeaendert,
  BelegDetail,
  Kategorie,
  KategorieAendern,
  KategorieGeloescht,
  Matching,
  MatchingAnwenden,
  MatchingTest,
  MatchingTestErgebnis,
  Tag,
  TagAendern,
} from '@/api/typen'
import { db, neueId } from '../db'
import { tagKurz } from '../fixtures/tags'
import { jsonKoerper, keinInhalt, nichtGefunden, problem, route, ungueltig, type Route } from './gemeinsam'

/** Fundstelle eines Musters im Text (wie die Matching-Regeln im Backend, vereinfacht). */
export function finde(matching: Matching, text: string): { start: number; laenge: number } | null {
  const muster = matching.muster?.trim()
  if (!muster) return null
  const flags = matching.grossKlein ? '' : 'i'
  const maskiert = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const woerter = muster.split(/\s+/)
  const suche = (ausdruck: RegExp) => {
    const treffer = ausdruck.exec(text)
    return treffer ? { start: treffer.index, laenge: treffer[0].length } : null
  }
  switch (matching.algorithmus) {
    case 'EinWort':
      return suche(new RegExp(woerter.map(maskiert).join('|'), flags))
    case 'AlleWoerter': {
      const fundstellen = woerter.map((w) => suche(new RegExp(maskiert(w), flags)))
      return fundstellen.every(Boolean) ? fundstellen[0] : null
    }
    case 'Exakt':
      return suche(new RegExp(maskiert(muster), flags))
    case 'Regex':
      try {
        return suche(new RegExp(muster, flags))
      } catch {
        return null
      }
    case 'Unscharf':
      return suche(new RegExp(maskiert(muster), 'i'))
    default:
      return null
  }
}

type Ziel = 'Tag' | 'Kategorie' | 'Korrespondent'

function zugewiesen(ziel: Ziel, beleg: BelegDetail, zielId?: string | null): boolean {
  if (ziel === 'Tag') return zielId ? beleg.tags.some((t) => t.id === zielId) : beleg.tags.length > 0
  if (ziel === 'Kategorie') return zielId ? beleg.kategorie?.id === zielId : !!beleg.kategorie
  return zielId ? beleg.korrespondent?.id === zielId : !!beleg.korrespondent
}

function matchingVon(ziel: string, zielId: string): Matching | undefined {
  if (ziel === 'Tag') return db.tags.find((t) => t.id === zielId)?.matching
  if (ziel === 'Kategorie') return db.kategorien.find((k) => k.id === zielId)?.matching
  return db.korrespondenten.find((k) => k.id === zielId)?.matching
}

function tagNachziehen(tag: Tag): void {
  db.belege = db.belege.map((b) => ({ ...b, tags: b.tags.map((t) => (t.id === tag.id ? tagKurz(tag) : t)) }))
}

function kategorieNachziehen(kategorie: Kategorie): void {
  const verweis = { id: kategorie.id, name: kategorie.name }
  db.belege = db.belege.map((b) => (b.kategorie?.id === kategorie.id ? { ...b, kategorie: verweis } : b))
}

const istZiel = (ziel: string): ziel is Ziel => ziel === 'Tag' || ziel === 'Kategorie' || ziel === 'Korrespondent'

export const tagsRouten: Route[] = [
  route('get', '/api/tags', 'Lesen', () => HttpResponse.json(db.tags)),

  route('post', '/api/tags', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<TagAendern>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    if (db.tags.some((t) => t.name.toLocaleLowerCase('de') === eingabe.name.trim().toLocaleLowerCase('de')))
      return ungueltig('name', 'Ein Tag mit diesem Namen gibt es schon.')
    const tag: Tag = { id: neueId(), ...eingabe, istSystem: false, anzahlBelege: 0 }
    db.tags.push(tag)
    return HttpResponse.json(tag, { status: 201 })
  }),

  route('put', '/api/tags/{id}', 'Bearbeiten', async ({ request, params }) => {
    const tag = db.tags.find((t) => t.id === params.id)
    if (!tag) return nichtGefunden('Tag')
    const eingabe = await jsonKoerper<TagAendern>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    // Der Systemtag „zu prüfen“ behält seine Sonderrolle; Inbox-Tag gibt es nur einmal.
    const neu: Tag = { ...tag, ...eingabe, istSystem: tag.istSystem }
    if (neu.istInbox) db.tags = db.tags.map((t) => (t.id !== neu.id && t.istInbox ? { ...t, istInbox: false } : t))
    db.tags = db.tags.map((t) => (t.id === neu.id ? neu : t))
    tagNachziehen(neu)
    return HttpResponse.json(neu)
  }),

  route('delete', '/api/tags/{id}', 'Bearbeiten', ({ params }) => {
    const tag = db.tags.find((t) => t.id === params.id)
    if (!tag) return nichtGefunden('Tag')
    if (tag.istSystem || tag.istInbox) return problem(409, 'Dieser Tag kann nicht gelöscht werden')
    db.tags = db.tags.filter((t) => t.id !== tag.id)
    db.belege = db.belege.map((b) => ({ ...b, tags: b.tags.filter((t) => t.id !== tag.id) }))
    return keinInhalt()
  }),

  route('get', '/api/kategorien', 'Lesen', () => HttpResponse.json(db.kategorien)),

  route('post', '/api/kategorien', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<KategorieAendern>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    const kategorie: Kategorie = { id: neueId(), ...eingabe, anzahlBelege: 0, summeBrutto: 0 }
    db.kategorien.push(kategorie)
    return HttpResponse.json(kategorie, { status: 201 })
  }),

  route('put', '/api/kategorien/{id}', 'Bearbeiten', async ({ request, params }) => {
    const kategorie = db.kategorien.find((k) => k.id === params.id)
    if (!kategorie) return nichtGefunden('Kategorie')
    const eingabe = await jsonKoerper<KategorieAendern>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    const neu: Kategorie = { ...kategorie, ...eingabe }
    db.kategorien = db.kategorien.map((k) => (k.id === neu.id ? neu : k))
    kategorieNachziehen(neu)
    return HttpResponse.json(neu)
  }),

  route('delete', '/api/kategorien/{id}', 'Bearbeiten', ({ params }) => {
    if (!db.kategorien.some((k) => k.id === params.id)) return nichtGefunden('Kategorie')
    const belege = db.belege.filter((b) => b.kategorie?.id === params.id).length
    const korrespondenten = db.korrespondenten.filter((k) => k.defaultKategorie?.id === params.id).length
    db.kategorien = db.kategorien.filter((k) => k.id !== params.id)
    db.belege = db.belege.map((b) => (b.kategorie?.id === params.id ? { ...b, kategorie: null } : b))
    db.korrespondenten = db.korrespondenten.map((k) =>
      k.defaultKategorie?.id === params.id ? { ...k, defaultKategorie: null } : k,
    )
    return HttpResponse.json({ belege, korrespondenten } satisfies KategorieGeloescht)
  }),

  route('post', '/api/matching/testen', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<MatchingTest>(request)
    if (!istZiel(eingabe.ziel)) return ungueltig('ziel', 'Ziel muss Tag, Kategorie oder Korrespondent sein.')
    const treffer = db.belege
      .map((beleg) => ({ beleg, fund: finde(eingabe.matching, beleg.auszug) }))
      .filter((t) => t.fund)
      .sort((a, b) => (b.beleg.rechnungsdatum ?? '').localeCompare(a.beleg.rechnungsdatum ?? ''))
    const erster = treffer[0]
    return HttpResponse.json<MatchingTestErgebnis>({
      treffer: treffer.length,
      gesamt: db.belege.length,
      ohneZuweisung: db.belege.filter((b) => !zugewiesen(eingabe.ziel as Ziel, b)).length,
      auszug: erster?.beleg.auszug ?? null,
      trefferStart: erster?.fund?.start ?? null,
      trefferLaenge: erster?.fund?.laenge ?? null,
      letzterTreffer: erster?.beleg.rechnungsnummer ?? null,
    })
  }),

  route('post', '/api/matching/anwenden', 'Bearbeiten', async ({ request }) => {
    const { ziel, zielId } = await jsonKoerper<MatchingAnwenden>(request)
    if (!istZiel(ziel)) return ungueltig('ziel', 'Ziel muss Tag, Kategorie oder Korrespondent sein.')
    const matching = matchingVon(ziel, zielId)
    if (!matching) return nichtGefunden(ziel)
    let geaendert = 0
    db.belege = db.belege.map((beleg) => {
      if (zugewiesen(ziel, beleg, zielId) || !finde(matching, beleg.auszug)) return beleg
      geaendert++
      if (ziel === 'Tag') return { ...beleg, tags: [...beleg.tags, tagKurz(db.tags.find((t) => t.id === zielId)!)] }
      if (ziel === 'Kategorie') {
        const k = db.kategorien.find((x) => x.id === zielId)!
        return { ...beleg, kategorie: { id: k.id, name: k.name } }
      }
      const k = db.korrespondenten.find((x) => x.id === zielId)!
      return { ...beleg, korrespondent: { id: k.id, name: k.name }, logo: k.logo }
    })
    return HttpResponse.json<AnzahlGeaendert>({ geaendert })
  }),

  route('get', '/api/matching/auto-status', 'Lesen', () => HttpResponse.json(db.autoMatching)),
]
