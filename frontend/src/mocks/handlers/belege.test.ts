import { describe, expect, it } from 'vitest'
import type { BelegDetail, BelegListe, BelegZaehler } from '@/api/typen'
import { db, zuruecksetzen } from '../db'
import { belegIds, tagIds } from '../fixtures/ids'
import { szenario } from '../szenarien'

const basis = `${location.origin}/api`

async function holeJson<T>(pfad: string, init?: RequestInit): Promise<T> {
  const antwort = await fetch(`${basis}${pfad}`, init)
  expect(antwort.ok).toBe(true)
  return (await antwort.json()) as T
}

const post = (pfad: string, koerper: unknown = {}) =>
  fetch(`${basis}${pfad}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(koerper),
  })

describe('Belege-Handler', () => {
  it('liefert für ansicht=ZuPruefen genau die fünf Belege des Designs', async () => {
    const liste = await holeJson<BelegListe>('/belege?ansicht=ZuPruefen')
    expect(liste.gesamt).toBe(5)
    expect(liste.eintraege.map((b) => `${b.korrespondent?.name} ${b.rechnungsnummer}`)).toEqual([
      'Bürowelt Krämer KG 2026-48817',
      'Stadtwerke Lindenau 2026-1187',
      'Druckerei Weller 26-0912',
      'Kanzlei Hofmann & Partner HP-2026-342',
      'Elektro Haas e.K. EH-4471',
    ])
    expect(liste.eintraege.every((b) => b.tags.some((t) => t.name === 'zu prüfen'))).toBe(true)
  })

  it('Bestätigen entfernt „zu prüfen“ im Mock-Zustand', async () => {
    const antwort = await post(`/belege/${belegIds.b2}/bestaetigen`, { begruendung: 'Versandkosten nachgerechnet' })
    expect(antwort.status).toBe(204)

    const beleg = await holeJson<BelegDetail>(`/belege/${belegIds.b2}`)
    expect(beleg.tags.map((t) => t.name)).toEqual(['Inbox'])
    const liste = await holeJson<BelegListe>('/belege?ansicht=ZuPruefen')
    expect(liste.gesamt).toBe(4)
    expect(liste.eintraege.some((b) => b.id === belegIds.b2)).toBe(false)
    const zaehler = await holeJson<BelegZaehler>('/belege/zaehler')
    expect(zaehler.zuPruefen).toBe(4)
  })

  it('zuruecksetzen() stellt die Beispieldaten wieder her', async () => {
    await post(`/belege/${belegIds.b2}/bestaetigen`)
    zuruecksetzen()
    const liste = await holeJson<BelegListe>('/belege?ansicht=ZuPruefen')
    expect(liste.gesamt).toBe(5)
  })

  it('zählt die Ansichten aus dem Zustand; überfällig sind die zwei Belege des Designs', async () => {
    const zaehler = await holeJson<BelegZaehler>('/belege/zaehler')
    expect(zaehler).toEqual({ alle: 12, inbox: 4, zuPruefen: 5, offen: 7, ueberfaellig: 2 })
    const ueberfaellig = await holeJson<BelegListe>('/belege?ansicht=Ueberfaellig')
    expect(ueberfaellig.eintraege.map((b) => b.rechnungsnummer)).toEqual(['2026-1102', 'EH-4471'])
    expect(ueberfaellig.eintraege.every((b) => b.ueberfaellig)).toBe(true)
  })

  it('filtert nach Tags, Zeitraum und Suche, sortiert und blättert', async () => {
    const bezahlt = await holeJson<BelegListe>(`/belege?tagIds=${tagIds.bez}&tagIds=${tagIds.pp}`)
    expect(bezahlt.eintraege.map((b) => b.rechnungsnummer)).toEqual(['R-118342', 'HVN-552190', '2026-47102'])

    const oktober = await holeJson<BelegListe>(
      '/belege?von=2026-10-01&bis=2026-10-31&sortierung=Brutto&absteigend=false',
    )
    expect(oktober.eintraege.map((b) => b.brutto)).toEqual([142, 178.5, 214, 489])
    expect(oktober.summeBrutto).toBe(1023.5)

    const suche = await holeJson<BelegListe>('/belege?suche=hostwerk')
    expect(suche.gesamt).toBe(2)

    const seite2 = await holeJson<BelegListe>('/belege?seite=2&seitengroesse=5')
    expect(seite2).toMatchObject({ gesamt: 12, seite: 2, seitengroesse: 5 })
    expect(seite2.eintraege).toHaveLength(5)
  })

  it('Tag hinzufügen und entfernen ändert den Beleg; „zu prüfen“ lässt sich nicht direkt entfernen', async () => {
    expect((await post(`/belege/${belegIds.b9}/tags`, { tagId: tagIds.bez })).status).toBe(204)
    expect((await holeJson<BelegDetail>(`/belege/${belegIds.b9}`)).ueberfaellig).toBe(false)

    const entfernen = await fetch(`${basis}/belege/${belegIds.b2}/tags/${tagIds.pruef}`, { method: 'DELETE' })
    expect(entfernen.status).toBe(409)
  })

  it('Massenaktion bestätigt mehrere Belege und meldet die Anzahl', async () => {
    const antwort = await post('/belege/massenaktion', {
      belegIds: [belegIds.b2, belegIds.b5, belegIds.b1],
      aktion: 'Bestaetigen',
    })
    expect(await antwort.json()).toEqual({ geaendert: 2 })
    expect((await holeJson<BelegZaehler>('/belege/zaehler')).zuPruefen).toBe(3)
  })

  it('liefert Datei, Vorschau und Historie', async () => {
    const beleg = await holeJson<BelegDetail>(`/belege/${belegIds.b2}`)
    const datei = await fetch(`${location.origin}${beleg.dateien[0].url}?inline=true`)
    expect(datei.headers.get('Content-Type')).toBe('application/pdf')
    expect((await datei.text()).startsWith('%PDF')).toBe(true)
    const vorschau = await fetch(`${basis}/belege/${belegIds.b2}/vorschau`)
    expect(vorschau.headers.get('Content-Type')).toBe('image/svg+xml')
    const historie = await holeJson<unknown[]>(`/belege/${belegIds.b2}/historie`)
    expect(historie).toHaveLength(8)
  })

  it('antwortet mit 401 ohne Sitzung, 403 für Nur-Lesen und erzwungenen Fehlern aus Szenarien', async () => {
    szenario.rolle('NurLesen')
    expect((await fetch(`${basis}/belege`)).status).toBe(200)
    expect((await post(`/belege/${belegIds.b2}/bestaetigen`)).status).toBe(403)

    szenario.fehler('/api/belege/{id}', 500, 'GET')
    const fehler = await fetch(`${basis}/belege/${belegIds.b1}`)
    expect(fehler.status).toBe(500)
    expect(await fehler.json()).toMatchObject({ status: 500 })

    szenario.abgemeldet()
    expect((await fetch(`${basis}/belege`)).status).toBe(401)
    expect(db.angemeldet).toBe(false)
  })
})
