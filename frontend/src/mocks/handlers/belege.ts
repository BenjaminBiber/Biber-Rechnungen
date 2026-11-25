import { HttpResponse } from 'msw'
import type {
  AnzahlGeaendert,
  AuditEintrag,
  Aufteilen,
  BelegAendern,
  BelegAnsicht,
  BelegDetail,
  BelegListe,
  BelegListeneintrag,
  BelegNachbarn,
  BelegSortierung,
  BelegZaehler,
  Bestaetigen,
  DubletteSetzen,
  GespeicherteAnsichtAnlegen,
  ImportQuelle,
  ImportVorgang,
  LogoAusschnitt,
  Massenaktion,
  PositionAendern,
  TagZuordnen,
  Zahlstatus,
} from '@/api/typen'
import { beispielPdf, beispielSeiteSvg, beispielVisualisierung, beispielXml } from '../dateien'
import { db, neueId } from '../db'
import { mockHeute } from '../fixtures/belege'
import { tagKurz } from '../fixtures/tags'
import {
  abfrage,
  angenommen,
  datei,
  jsonKoerper,
  keinInhalt,
  nichtGefunden,
  problem,
  route,
  runden,
  ungueltig,
  wahr,
  zahl,
  type Route,
} from './gemeinsam'

// ---------------------------------------------------------------------------------------------
// Ableitungen, die auch andere Bereiche (Dashboard, Korrespondenten, Suche) nutzen
// ---------------------------------------------------------------------------------------------

export function istBezahlt(beleg: BelegDetail): boolean {
  return beleg.tags.some((t) => t.istZahlungsTag)
}

export function istUeberfaellig(beleg: BelegDetail): boolean {
  return !istBezahlt(beleg) && !!beleg.faelligkeitsdatum && beleg.faelligkeitsdatum < mockHeute
}

export function istZuPruefen(beleg: BelegDetail): boolean {
  return beleg.tags.some((t) => t.istSystem)
}

export function istInbox(beleg: BelegDetail): boolean {
  return beleg.tags.some((t) => t.istInbox)
}

/** Listeneintrag aus dem gespeicherten Detail; `ueberfaellig` wird immer neu berechnet. */
export function listeneintrag(beleg: BelegDetail): BelegListeneintrag {
  return {
    id: beleg.id,
    rechnungsnummer: beleg.rechnungsnummer,
    korrespondent: beleg.korrespondent,
    logo: beleg.logo,
    titel: beleg.titel,
    kategorie: beleg.kategorie,
    rechnungsdatum: beleg.rechnungsdatum,
    faelligkeitsdatum: beleg.faelligkeitsdatum,
    ueberfaellig: istUeberfaellig(beleg),
    netto: beleg.netto,
    brutto: beleg.brutto,
    waehrung: beleg.waehrung,
    seitenanzahl: beleg.seitenanzahl,
    quelle: beleg.quelle,
    format: beleg.format,
    tags: beleg.tags,
    auszug: beleg.auszug,
    vorschauUrl: beleg.vorschauUrl,
    istDublette: beleg.istDublette,
    konfidenz: beleg.konfidenz,
    leistungsdatum: beleg.leistungsdatum,
  }
}

function detail(beleg: BelegDetail): BelegDetail {
  return { ...beleg, ueberfaellig: istUeberfaellig(beleg) }
}

function passtZurAnsicht(beleg: BelegDetail, ansicht: BelegAnsicht): boolean {
  switch (ansicht) {
    case 'Inbox':
      return istInbox(beleg)
    case 'ZuPruefen':
      return istZuPruefen(beleg)
    case 'Offen':
      return !istBezahlt(beleg)
    case 'Ueberfaellig':
      return istUeberfaellig(beleg)
    default:
      return true
  }
}

function passtZumZahlstatus(beleg: BelegDetail, status: Zahlstatus): boolean {
  if (status === 'Bezahlt') return istBezahlt(beleg)
  if (status === 'Ueberfaellig') return istUeberfaellig(beleg)
  return !istBezahlt(beleg)
}

const sortierwert: Record<BelegSortierung, (b: BelegDetail) => string | number> = {
  Rechnungsdatum: (b) => b.rechnungsdatum ?? '',
  Faelligkeit: (b) => b.faelligkeitsdatum ?? '',
  Brutto: (b) => b.brutto ?? 0,
  Korrespondent: (b) => b.korrespondent?.name.toLocaleLowerCase('de') ?? '',
  // Belege tragen kein Erstellungsdatum; die Ablage folgt dem Rechnungsdatum.
  ErstelltAm: (b) => b.extraktion.zeitpunkt ?? b.rechnungsdatum ?? '',
  Rechnungsnummer: (b) => b.rechnungsnummer ?? '',
  Titel: (b) => (b.titel || b.rechnungsnummer || b.korrespondent?.name || '').toLocaleLowerCase('de'),
  Kategorie: (b) => b.kategorie?.name.toLocaleLowerCase('de') ?? '',
  Netto: (b) => b.netto ?? 0,
  Seiten: (b) => b.seitenanzahl,
  // Wie im Backend nach dem Enum-Namen (Mail, Mcp, Ordner, Upload).
  Herkunft: (b) => b.quelle,
  Leistungsdatum: (b) => b.leistungsdatum ?? '',
  Konfidenz: (b) => b.konfidenz ?? 0,
}

/** Filtert und sortiert wie `GET /api/belege` (ohne Blättern). */
export function filtereBelege(parameter: URLSearchParams): BelegDetail[] {
  const suche = parameter.get('suche')?.trim().toLocaleLowerCase('de')
  const suchfeld = parameter.get('suchfeld') ?? 'TitelInhalt'
  const ansicht = (parameter.get('ansicht') ?? 'Alle') as BelegAnsicht
  const tagIds = parameter.getAll('tagIds')
  const korrespondentIds = parameter.getAll('korrespondentIds')
  const kategorieIds = parameter.getAll('kategorieIds')
  const quellen = parameter.getAll('quellen') as ImportQuelle[]
  const von = parameter.get('von')
  const bis = parameter.get('bis')
  const betragVon = zahl(parameter, 'betragVon')
  const betragBis = zahl(parameter, 'betragBis')
  const betragArt = parameter.get('betragArt') ?? 'Brutto'
  const zahlstatus = parameter.get('zahlstatus') as Zahlstatus | null
  const ohneDubletten = wahr(parameter, 'dublettenAusschliessen') ?? true
  const sortierung = (parameter.get('sortierung') ?? 'Rechnungsdatum') as BelegSortierung
  const absteigend = wahr(parameter, 'absteigend') ?? true

  const treffer = db.belege.filter((b) => {
    if (!passtZurAnsicht(b, ansicht)) return false
    if (suche) {
      const felder =
        suchfeld === 'Rechnungsnummer'
          ? [b.rechnungsnummer]
          : [b.titel, b.auszug, b.rechnungsnummer, b.korrespondent?.name, b.kategorie?.name]
      if (!felder.some((f) => f?.toLocaleLowerCase('de').includes(suche))) return false
    }
    if (tagIds.length && !b.tags.some((t) => tagIds.includes(t.id))) return false
    if (korrespondentIds.length && !korrespondentIds.includes(b.korrespondent?.id ?? '')) return false
    if (kategorieIds.length && !kategorieIds.includes(b.kategorie?.id ?? '')) return false
    if (quellen.length && !quellen.includes(b.quelle)) return false
    if (von && (b.rechnungsdatum ?? '') < von) return false
    if (bis && (b.rechnungsdatum ?? '') > bis) return false
    const betrag = (betragArt === 'Netto' ? b.netto : b.brutto) ?? 0
    if (betragVon !== undefined && betrag < betragVon) return false
    if (betragBis !== undefined && betrag > betragBis) return false
    if (zahlstatus && !passtZumZahlstatus(b, zahlstatus)) return false
    if (ohneDubletten && b.istDublette) return false
    return true
  })
  const wert = sortierwert[sortierung] ?? sortierwert.Rechnungsdatum
  return treffer.sort((a, b) => {
    const x = wert(a)
    const y = wert(b)
    const vergleich = typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), 'de')
    return absteigend ? -vergleich : vergleich
  })
}

export function findeBeleg(id: string): BelegDetail | undefined {
  return db.belege.find((b) => b.id === id)
}

/** Inhalt der Beispielseite (Vorschaubild, Bild-Original) eines Belegs. */
function seitenDaten(beleg: BelegDetail) {
  return {
    absender: beleg.korrespondent?.name ?? 'Unbekannter Absender',
    titel: beleg.titel,
    nummer: beleg.rechnungsnummer ?? '—',
    betrag:
      beleg.brutto === null || beleg.brutto === undefined
        ? '—'
        : `${beleg.brutto.toFixed(2).replace('.', ',')} ${beleg.waehrung === 'EUR' ? '€' : beleg.waehrung}`,
  }
}

function ersetze(beleg: BelegDetail): BelegDetail {
  db.belege = db.belege.map((b) => (b.id === beleg.id ? beleg : b))
  return beleg
}

/** Neuer Historien-Eintrag des angemeldeten Benutzers (oben = neuester). */
function protokolliere(
  belegId: string,
  text: string,
  art = 'bearbeitet',
  feld?: string,
  alt?: string,
  neu?: string,
): void {
  const eintrag: AuditEintrag = {
    id: neueId(),
    zeitpunkt: new Date().toISOString(),
    wer: db.benutzer.name,
    istSystem: false,
    aktion: 'Geändert',
    text,
    feld: feld ?? null,
    alterWert: alt ?? null,
    neuerWert: neu ?? null,
    art,
  }
  db.historien[belegId] = [eintrag, ...(db.historien[belegId] ?? [])]
}

function tagSetzen(beleg: BelegDetail, tagId: string): BelegDetail {
  if (beleg.tags.some((t) => t.id === tagId)) return beleg
  const tag = db.tags.find((t) => t.id === tagId)
  if (!tag) return beleg
  return { ...beleg, tags: [...beleg.tags, tagKurz(tag)] }
}

function tagLoesen(beleg: BelegDetail, tagId: string): BelegDetail {
  return { ...beleg, tags: beleg.tags.filter((t) => t.id !== tagId) }
}

function bestaetige(beleg: BelegDetail, begruendung?: string | null): BelegDetail {
  const ohne = { ...beleg, tags: beleg.tags.filter((t) => !t.istSystem) }
  protokolliere(beleg.id, begruendung ? `Bestätigt: ${begruendung}` : 'Prüfung bestätigt', 'bestaetigt')
  return ohne
}

function verweis(liste: { id: string; name: string }[], id: string | null | undefined) {
  if (!id) return null
  const treffer = liste.find((e) => e.id === id)
  return treffer ? { id: treffer.id, name: treffer.name } : undefined
}

// ---------------------------------------------------------------------------------------------
// Routen
// ---------------------------------------------------------------------------------------------

export const belegeRouten: Route[] = [
  route('get', '/api/belege', 'Lesen', ({ request }) => {
    const parameter = abfrage(request)
    const treffer = filtereBelege(parameter)
    const seite = Math.max(1, zahl(parameter, 'seite') ?? 1)
    const groesse = Math.max(1, zahl(parameter, 'seitengroesse') ?? 25)
    const start = (seite - 1) * groesse
    return HttpResponse.json<BelegListe>({
      eintraege: treffer.slice(start, start + groesse).map(listeneintrag),
      gesamt: treffer.length,
      seite,
      seitengroesse: groesse,
      summeBrutto: runden(treffer.reduce((summe, b) => summe + (b.brutto ?? 0), 0)),
    })
  }),

  route('get', '/api/belege/zaehler', 'Lesen', () =>
    HttpResponse.json<BelegZaehler>({
      alle: db.belege.length,
      inbox: db.belege.filter(istInbox).length,
      zuPruefen: db.belege.filter(istZuPruefen).length,
      offen: db.belege.filter((b) => !istBezahlt(b)).length,
      ueberfaellig: db.belege.filter(istUeberfaellig).length,
    }),
  ),

  route('post', '/api/belege/massenaktion', 'Bearbeiten', async ({ request }) => {
    const eingabe = await jsonKoerper<Massenaktion>(request)
    if (!eingabe.belegIds.length) return ungueltig('belegIds', 'Keine Belege ausgewählt.')
    if (eingabe.aktion === 'KategorieSetzen' && !verweis(db.kategorien, eingabe.kategorieId))
      return ungueltig('kategorieId', 'Kategorie fehlt oder ist unbekannt.')
    let geaendert = 0
    for (const id of eingabe.belegIds) {
      const beleg = findeBeleg(id)
      if (!beleg) continue
      let neu = beleg
      switch (eingabe.aktion) {
        case 'TagsSetzen':
          neu = (eingabe.tagIds ?? []).reduce(tagSetzen, beleg)
          break
        case 'TagsEntfernen':
          neu = (eingabe.tagIds ?? []).reduce(tagLoesen, beleg)
          break
        case 'KategorieSetzen':
          neu = { ...beleg, kategorie: verweis(db.kategorien, eingabe.kategorieId) ?? null, kategorieManuell: true }
          break
        case 'Bestaetigen':
          neu = istZuPruefen(beleg) ? bestaetige(beleg) : beleg
          break
      }
      if (
        neu !== beleg &&
        JSON.stringify(neu.tags) + neu.kategorie?.id !== JSON.stringify(beleg.tags) + beleg.kategorie?.id
      ) {
        ersetze(neu)
        geaendert++
      }
    }
    return HttpResponse.json<AnzahlGeaendert>({ geaendert })
  }),

  route('get', '/api/belege/{id}', 'Lesen', ({ params }) => {
    const beleg = findeBeleg(params.id)
    return beleg ? HttpResponse.json(detail(beleg)) : nichtGefunden('Beleg')
  }),

  route('put', '/api/belege/{id}', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const eingabe = await jsonKoerper<BelegAendern>(request)
    if (!eingabe.titel?.trim()) return ungueltig('titel', 'Titel ist erforderlich.')
    const korrespondent = verweis(db.korrespondenten, eingabe.korrespondentId)
    if (korrespondent === undefined) return ungueltig('korrespondentId', 'Unbekannter Korrespondent.')
    const kategorie = verweis(db.kategorien, eingabe.kategorieId)
    if (kategorie === undefined) return ungueltig('kategorieId', 'Unbekannte Kategorie.')
    const logo = korrespondent ? db.korrespondenten.find((k) => k.id === korrespondent.id)!.logo : beleg.logo
    if (beleg.kategorie?.id !== kategorie?.id)
      protokolliere(beleg.id, 'Kategorie geändert', 'bearbeitet', 'Kategorie', beleg.kategorie?.name, kategorie?.name)
    if (beleg.korrespondent?.id !== korrespondent?.id)
      protokolliere(
        beleg.id,
        'Korrespondent geändert',
        'bearbeitet',
        'Korrespondent',
        beleg.korrespondent?.name,
        korrespondent?.name,
      )
    const neu: BelegDetail = {
      ...beleg,
      korrespondent,
      logo,
      korrespondentErkennung: beleg.korrespondent?.id !== korrespondent?.id ? 'Manuell' : beleg.korrespondentErkennung,
      belegart: eingabe.belegart,
      rechnungsnummer: eingabe.rechnungsnummer ?? null,
      titel: eingabe.titel,
      rechnungsdatum: eingabe.rechnungsdatum ?? null,
      leistungsdatum: eingabe.leistungsdatum ?? null,
      leistungVon: eingabe.leistungVon ?? null,
      leistungBis: eingabe.leistungBis ?? null,
      faelligkeitsdatum: eingabe.faelligkeitsdatum ?? null,
      kategorie,
      kategorieManuell: beleg.kategorie?.id !== kategorie?.id ? true : beleg.kategorieManuell,
      waehrung: eingabe.waehrung,
      netto: eingabe.netto ?? null,
      steuer: eingabe.steuer ?? null,
      brutto: eingabe.brutto ?? null,
      steuerzeilen: eingabe.steuerzeilen.map((z) => ({
        ...z,
        stimmt: Math.abs(runden((z.netto * z.satz) / 100) - z.steuer) <= 0.02,
      })),
    }
    return HttpResponse.json(detail(ersetze(neu)))
  }),

  route('get', '/api/belege/{id}/nachbarn', 'Lesen', ({ request, params }) => {
    if (!findeBeleg(params.id)) return nichtGefunden('Beleg')
    const liste = filtereBelege(abfrage(request))
    const index = liste.findIndex((b) => b.id === params.id)
    return HttpResponse.json<BelegNachbarn>({
      vorherId: index > 0 ? liste[index - 1].id : null,
      naechsteId: index >= 0 && index < liste.length - 1 ? liste[index + 1].id : null,
      position: index + 1,
      gesamt: liste.length,
    })
  }),

  route('get', '/api/belege/{id}/aehnliche', 'Lesen', ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const anzahl = zahl(abfrage(request), 'anzahl') ?? 5
    const aehnliche = db.belege
      .filter((b) => b.id !== beleg.id && b.korrespondent?.id === beleg.korrespondent?.id)
      .sort((a, b) => Math.abs((a.brutto ?? 0) - (beleg.brutto ?? 0)) - Math.abs((b.brutto ?? 0) - (beleg.brutto ?? 0)))
      .slice(0, anzahl)
    return HttpResponse.json(aehnliche.map(listeneintrag))
  }),

  route('get', '/api/belege/{id}/historie', 'Lesen', ({ params }) =>
    findeBeleg(params.id) ? HttpResponse.json(db.historien[params.id] ?? []) : nichtGefunden('Beleg'),
  ),

  route('get', '/api/belege/{id}/dateien/{dateiId}', 'Lesen', ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    const eintrag = beleg?.dateien.find((d) => d.id === params.dateiId)
    if (!beleg || !eintrag) return nichtGefunden('Datei')
    const inline = wahr(abfrage(request), 'inline') ?? false
    const name = eintrag.pfad.split('/').pop()
    if (eintrag.mimeType === 'application/xml')
      return datei(beispielXml(beleg.rechnungsnummer ?? ''), eintrag.mimeType, name, { inline })
    if (eintrag.mimeType === 'text/html')
      return datei(beispielVisualisierung(beleg.titel), eintrag.mimeType, name, { inline })
    if (eintrag.mimeType.startsWith('image/'))
      return datei(beispielSeiteSvg(seitenDaten(beleg)), 'image/svg+xml', name, { inline })
    return datei(beispielPdf, 'application/pdf', name, { inline })
  }),

  route('get', '/api/belege/{id}/vorschau', 'Lesen', ({ params }) => {
    const beleg = findeBeleg(params.id)
    return beleg ? datei(beispielSeiteSvg(seitenDaten(beleg)), 'image/svg+xml') : nichtGefunden('Beleg')
  }),

  route('get', '/api/belege/{id}/visualisierung', 'Lesen', ({ params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    if (!beleg.istERechnung) return nichtGefunden('Visualisierung')
    return datei(beispielVisualisierung(beleg.titel), 'text/html')
  }),

  route('put', '/api/belege/{id}/positionen', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const eingabe = await jsonKoerper<PositionAendern[]>(request)
    const positionen = eingabe.map((p) => {
      const alt = beleg.positionen.find((q) => q.text === p.text)
      return {
        ...p,
        artikel: alt?.artikel ?? null,
        stammartikelId: alt?.stammartikelId ?? null,
        grundpreis: alt?.grundpreis ?? null,
        grundpreisEinheit: alt?.grundpreisEinheit ?? null,
      }
    })
    protokolliere(
      beleg.id,
      'Positionen geändert',
      'bearbeitet',
      'Positionen',
      String(beleg.positionen.length),
      String(positionen.length),
    )
    return HttpResponse.json(detail(ersetze({ ...beleg, positionen })))
  }),

  route('post', '/api/belege/{id}/tags', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const { tagId } = await jsonKoerper<TagZuordnen>(request)
    const tag = db.tags.find((t) => t.id === tagId)
    if (!tag) return ungueltig('tagId', 'Unbekannter Tag.')
    if (tag.istSystem) return problem(409, 'Systemtags setzt nur die Pipeline')
    ersetze(tagSetzen(beleg, tagId))
    protokolliere(beleg.id, `Tag hinzugefügt: ${tag.name}`, 'tags')
    return keinInhalt()
  }),

  route('delete', '/api/belege/{id}/tags/{tagId}', 'Bearbeiten', ({ params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const tag = beleg.tags.find((t) => t.id === params.tagId)
    if (!tag) return nichtGefunden('Tag am Beleg')
    if (tag.istSystem) return problem(409, '„zu prüfen“ wird über „Bestätigen“ entfernt')
    ersetze(tagLoesen(beleg, params.tagId))
    protokolliere(beleg.id, `Tag entfernt: ${tag.name}`, 'tags')
    return keinInhalt()
  }),

  route('post', '/api/belege/{id}/bestaetigen', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const eingabe = await jsonKoerper<Bestaetigen>(request).catch(() => ({}) as Bestaetigen)
    ersetze(bestaetige(beleg, eingabe.begruendung))
    return keinInhalt()
  }),

  route('post', '/api/belege/{id}/erneut-auslesen', 'Bearbeiten', ({ params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    protokolliere(beleg.id, 'Erneut auslesen angestoßen', 'auslesen')
    return angenommen()
  }),

  route('post', '/api/belege/{id}/dublette', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const { duplikatVonId } = await jsonKoerper<DubletteSetzen>(request)
    const original = duplikatVonId ? findeBeleg(duplikatVonId) : null
    if (duplikatVonId && !original) return ungueltig('duplikatVonId', 'Unbekannter Beleg.')
    if (original?.id === beleg.id)
      return ungueltig('duplikatVonId', 'Ein Beleg kann nicht Dublette von sich selbst sein.')
    ersetze({
      ...beleg,
      istDublette: !!original,
      dubletten: {
        ...beleg.dubletten,
        duplikatVon: original ? { id: original.id, name: original.rechnungsnummer ?? original.titel } : null,
      },
    })
    protokolliere(
      beleg.id,
      original ? `Als Dublette von ${original.rechnungsnummer} markiert` : 'Dubletten-Markierung aufgehoben',
    )
    return keinInhalt()
  }),

  route('post', '/api/belege/{id}/aufteilen', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const { teile } = await jsonKoerper<Aufteilen>(request)
    if (teile.length < 2) return ungueltig('teile', 'Mindestens zwei Teile angeben.')
    const seiten = teile.flatMap((t) => t.seiten)
    if (seiten.some((s) => s < 1 || s > beleg.seitenanzahl)) return ungueltig('teile', 'Seite außerhalb des Belegs.')
    const vorgaenge: ImportVorgang[] = teile.map((teil, i) => ({
      id: neueId(),
      zeitpunkt: new Date().toISOString(),
      quelle: beleg.quelle,
      dateiname: `${beleg.rechnungsnummer ?? 'Beleg'}_Teil${i + 1}.pdf`,
      groesse: 50_000 * teil.seiten.length,
      herkunft: db.benutzer.name,
      herkunftDetail: `Aufgeteilt aus ${beleg.rechnungsnummer ?? beleg.titel}, Seiten ${teil.seiten.join(', ')}`,
      status: 'Angenommen',
      versuche: 0,
      maxVersuche: 5,
      ergebnis: null,
      fehler: null,
      belegId: null,
      duplikatVonBelegId: null,
      naechsterVersuchAm: null,
      schritte: [],
    }))
    db.importVorgaenge = [...vorgaenge, ...db.importVorgaenge]
    db.simulierteVorgaenge.push(...vorgaenge.map((v) => v.id))
    protokolliere(beleg.id, `Aufgeteilt in ${teile.length} Teile`, 'aufgeteilt')
    return HttpResponse.json(vorgaenge, { status: 202 })
  }),

  route('post', '/api/belege/{id}/logo-uebernehmen', 'Bearbeiten', async ({ request, params }) => {
    const beleg = findeBeleg(params.id)
    if (!beleg) return nichtGefunden('Beleg')
    const ausschnitt = await jsonKoerper<LogoAusschnitt>(request)
    if (ausschnitt.breite <= 0 || ausschnitt.hoehe <= 0) return ungueltig('breite', 'Der Ausschnitt ist leer.')
    const korrespondent = db.korrespondenten.find((k) => k.id === beleg.korrespondent?.id)
    if (!korrespondent) return problem(409, 'Beleg hat keinen Korrespondenten')
    const logo = { ...korrespondent.logo, url: `/api/korrespondenten/${korrespondent.id}/logo`, quelle: 'Pdf' as const }
    korrespondent.logo = logo
    db.belege = db.belege.map((b) => (b.korrespondent?.id === korrespondent.id ? { ...b, logo } : b))
    return HttpResponse.json(logo)
  }),

  // Gespeicherte Ansichten
  route('get', '/api/ansichten', 'Lesen', () => HttpResponse.json(db.ansichten)),

  route('post', '/api/ansichten', 'Lesen', async ({ request }) => {
    const eingabe = await jsonKoerper<GespeicherteAnsichtAnlegen>(request)
    if (!eingabe.name?.trim()) return ungueltig('name', 'Name ist erforderlich.')
    const ansicht = { id: neueId(), ...eingabe }
    db.ansichten.push(ansicht)
    return HttpResponse.json(ansicht, { status: 201 })
  }),

  route('delete', '/api/ansichten/{id}', 'Lesen', ({ params }) => {
    if (!db.ansichten.some((a) => a.id === params.id)) return nichtGefunden('Ansicht')
    db.ansichten = db.ansichten.filter((a) => a.id !== params.id)
    return keinInhalt()
  }),
]
