import { HttpResponse } from 'msw'
import type {
  KorrespondentAendern,
  KorrespondentDetail,
  KorrespondentenZusammenfuehren,
  KorrespondentListeneintrag,
  LogoQuelle,
} from '@/api/typen'
import { beispielLogoSvg } from '../dateien'
import { db, neueId } from '../db'
import { logoFuer, type KorrespondentDatensatz } from '../fixtures/korrespondenten'
import { istBezahlt, listeneintrag } from './belege'
import {
  abfrage,
  datei,
  jsonKoerper,
  keinInhalt,
  nichtGefunden,
  problem,
  route,
  ungueltig,
  type Route,
} from './gemeinsam'

export function korrespondentEintrag(k: KorrespondentDatensatz): KorrespondentListeneintrag {
  return {
    id: k.id,
    name: k.name,
    logo: k.logo,
    hinweis: k.hinweis,
    ustIdNr: k.ustIdNr,
    steuernummer: k.steuernummer,
    defaultKategorie: k.defaultKategorie,
    matching: k.matching,
    anzahlBelege: k.anzahlBelege,
    kostenJahr: k.kostenJahr,
    letzterBeleg: k.letzterBeleg,
    istNeu: k.istNeu,
  }
}

/** Detail mit den aus Belegen und Artikeln abgeleiteten Teilen (offen, letzte Belege, Artikel). */
function korrespondentDetail(k: KorrespondentDatensatz): KorrespondentDetail {
  const {
    logoFarben: _logoFarben,
    hinweis: _hinweis,
    anzahlBelege: _a,
    kostenJahr: _k,
    letzterBeleg: _l,
    istNeu: _n,
    ...rest
  } = k
  const eigene = db.belege
    .filter((b) => b.korrespondent?.id === k.id)
    .sort((a, b) => (b.rechnungsdatum ?? '').localeCompare(a.rechnungsdatum ?? ''))
  const offen = eigene.filter((b) => !istBezahlt(b))
  const artikel = db.artikel.filter((a) => a.korrespondent.id === k.id)
  return {
    ...rest,
    kennzahlen: {
      ...k.kennzahlen,
      offenAnzahl: offen.length,
      offenSumme: Math.round(offen.reduce((s, b) => s + (b.brutto ?? 0), 0) * 100) / 100,
      naechsteFaelligkeit:
        offen
          .map((b) => b.faelligkeitsdatum)
          .filter((d): d is string => !!d)
          .sort()[0] ?? null,
    },
    letzteBelege: eigene.slice(0, 5).map(listeneintrag),
    artikel,
    anzahlArtikel: artikel.length,
  }
}

function finde(id: string): KorrespondentDatensatz | undefined {
  return db.korrespondenten.find((k) => k.id === id)
}

const ustNormalisiert = (ust: string) => ust.replace(/\s+/g, '').toUpperCase()

/** Prüft wie die API; eine schon vergebene USt-IdNr. ist 409 mit `korrespondentId` des vorhandenen. */
function pruefe(eingabe: KorrespondentAendern, eigeneId?: string): Response | undefined {
  if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
  if (eingabe.defaultKategorieId && !db.kategorien.some((k) => k.id === eingabe.defaultKategorieId))
    return ungueltig('defaultKategorieId', 'Unbekannte Kategorie.')
  const ust = eingabe.ustIdNr?.trim() ? ustNormalisiert(eingabe.ustIdNr) : null
  const vorhanden = ust
    ? db.korrespondenten.find((k) => k.id !== eigeneId && k.ustIdNr && ustNormalisiert(k.ustIdNr) === ust)
    : undefined
  if (vorhanden)
    return problem(409, 'Konflikt', undefined, {
      detail: `Die USt-IdNr. ${ust} gehört bereits zu „${vorhanden.name}“.`,
      korrespondentId: vorhanden.id,
    })
  return undefined
}

function uebernehme(k: KorrespondentDatensatz, eingabe: KorrespondentAendern): KorrespondentDatensatz {
  const kategorie = db.kategorien.find((x) => x.id === eingabe.defaultKategorieId)
  return {
    ...k,
    name: eingabe.name,
    aliase: eingabe.aliase,
    anschrift: eingabe.anschrift ?? null,
    ustIdNr: eingabe.ustIdNr ?? null,
    steuernummer: eingabe.steuernummer ?? null,
    bankverbindungen: eingabe.bankverbindungen,
    website: eingabe.website ?? null,
    email: eingabe.email ?? null,
    istLieferant: eingabe.istLieferant,
    istKunde: eingabe.istKunde,
    defaultKategorie: kategorie ? { id: kategorie.id, name: kategorie.name } : null,
    matching: eingabe.matching,
  }
}

/** Name und Logo in allen Belegen des Korrespondenten nachziehen. */
function belegeNachziehen(k: KorrespondentDatensatz): void {
  db.belege = db.belege.map((b) =>
    b.korrespondent?.id === k.id ? { ...b, korrespondent: { id: k.id, name: k.name }, logo: k.logo } : b,
  )
}

function setzeLogo(k: KorrespondentDatensatz, quelle: LogoQuelle) {
  k.logo = logoFuer(k.id, k.logo.initialen, quelle)
  belegeNachziehen(k)
  return k.logo
}

function initialen(name: string): string {
  return name
    .split(/\s+/)
    .filter((w) => /^[A-Za-zÄÖÜäöü]/.test(w))
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join('')
}

export const korrespondentenRouten: Route[] = [
  route('get', '/api/korrespondenten', 'Lesen', ({ request }) => {
    const parameter = abfrage(request)
    const suche = parameter.get('suche')?.trim().toLocaleLowerCase('de')
    const typ = parameter.get('typ') ?? 'Alle'
    const treffer = db.korrespondenten.filter((k) => {
      if (typ === 'Lieferanten' && !k.istLieferant) return false
      if (typ === 'Kunden' && !k.istKunde) return false
      if (suche && ![k.name, ...k.aliase].some((n) => n.toLocaleLowerCase('de').includes(suche))) return false
      return true
    })
    return HttpResponse.json(treffer.map(korrespondentEintrag))
  }),

  route('post', '/api/korrespondenten', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<KorrespondentAendern>(request)
    const fehler = pruefe(eingabe)
    if (fehler) return fehler
    const id = neueId()
    const neu = uebernehme(
      {
        id,
        name: eingabe.name,
        aliase: [],
        anschrift: null,
        ustIdNr: null,
        steuernummer: null,
        bankverbindungen: [],
        website: null,
        email: null,
        istLieferant: true,
        istKunde: false,
        logo: logoFuer(id, initialen(eingabe.name), 'Keine'),
        logoFarben: { hintergrund: '#EEF0F3', schrift: '#4A5262' },
        defaultKategorie: null,
        matching: eingabe.matching,
        autoTrainiertAm: null,
        kennzahlen: {
          kostenJahr: 0,
          aenderungVorjahr: null,
          anzahlBelege: 0,
          seit: null,
          durchschnittJeBeleg: 0,
          offenAnzahl: 0,
          offenSumme: 0,
          naechsteFaelligkeit: null,
        },
        kostenverlaufMonate: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        hinweis: null,
        anzahlBelege: 0,
        kostenJahr: 0,
        letzterBeleg: null,
        istNeu: true,
      },
      eingabe,
    )
    db.korrespondenten.push(neu)
    return HttpResponse.json(korrespondentDetail(neu), { status: 201 })
  }),

  route('post', '/api/korrespondenten/zusammenfuehren', 'Bearbeiten', async ({ request }) => {
    const { behaltenId, quellIds } = await jsonKoerper<KorrespondentenZusammenfuehren>(request)
    const behalten = finde(behaltenId)
    if (!behalten) return ungueltig('behaltenId', 'Unbekannter Korrespondent.')
    const quellen = quellIds.filter((id) => id !== behaltenId).map(finde)
    if (!quellen.length || quellen.some((q) => !q)) return ungueltig('quellIds', 'Unbekannte oder fehlende Quellen.')
    for (const quelle of quellen as KorrespondentDatensatz[]) {
      behalten.aliase = [...new Set([...behalten.aliase, quelle.name, ...quelle.aliase])]
      behalten.anzahlBelege += quelle.anzahlBelege
      behalten.kostenJahr = Math.round((behalten.kostenJahr + quelle.kostenJahr) * 100) / 100
      db.belege = db.belege.map((b) =>
        b.korrespondent?.id === quelle.id ? { ...b, korrespondent: { id: behalten.id, name: behalten.name } } : b,
      )
    }
    behalten.kennzahlen = {
      ...behalten.kennzahlen,
      anzahlBelege: behalten.anzahlBelege,
      kostenJahr: behalten.kostenJahr,
    }
    db.korrespondenten = db.korrespondenten.filter((k) => !quellIds.includes(k.id) || k.id === behaltenId)
    belegeNachziehen(behalten)
    return HttpResponse.json(korrespondentDetail(behalten))
  }),

  route('get', '/api/korrespondenten/{id}', 'Lesen', ({ params }) => {
    const k = finde(params.id)
    return k ? HttpResponse.json(korrespondentDetail(k)) : nichtGefunden('Korrespondent')
  }),

  route('put', '/api/korrespondenten/{id}', 'Bearbeiten', async ({ request, params }) => {
    const k = finde(params.id)
    if (!k) return nichtGefunden('Korrespondent')
    const eingabe = await jsonKoerper<KorrespondentAendern>(request)
    const fehler = pruefe(eingabe, k.id)
    if (fehler) return fehler
    const neu = uebernehme(k, eingabe)
    db.korrespondenten = db.korrespondenten.map((x) => (x.id === k.id ? neu : x))
    belegeNachziehen(neu)
    return HttpResponse.json(korrespondentDetail(neu))
  }),

  route('get', '/api/korrespondenten/{id}/logo', 'Lesen', ({ params }) => {
    const k = finde(params.id)
    if (!k || k.logo.quelle === 'Keine') return nichtGefunden('Logo')
    return datei(beispielLogoSvg(k.logo.initialen, k.name), 'image/svg+xml')
  }),

  route('put', '/api/korrespondenten/{id}/logo', 'Bearbeiten', async ({ request, params }) => {
    const k = finde(params.id)
    if (!k) return nichtGefunden('Korrespondent')
    const formular = await request.formData()
    if (!formular.get('datei')) return ungueltig('datei', 'Keine Datei übermittelt.')
    return HttpResponse.json(setzeLogo(k, 'Upload'))
  }),

  route('delete', '/api/korrespondenten/{id}/logo', 'Bearbeiten', ({ params }) => {
    const k = finde(params.id)
    if (!k) return nichtGefunden('Korrespondent')
    setzeLogo(k, 'Keine')
    return keinInhalt()
  }),

  route('post', '/api/korrespondenten/{id}/logo/favicon', 'Bearbeiten', ({ params }) => {
    const k = finde(params.id)
    if (!k) return nichtGefunden('Korrespondent')
    return HttpResponse.json(setzeLogo(k, 'Favicon'))
  }),
]
