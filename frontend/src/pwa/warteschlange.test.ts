import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  ablegen,
  aktualisieren,
  alleWartendenSenden,
  aufraeumen,
  auflisten,
  beanspruchen,
  entfernen,
  lesen,
  schliessen,
  sendenPerFetch,
  sichtbarNachUebertragung,
} from './warteschlange'

const pdf = (name: string) => new File(['%PDF-1.4'], name, { type: 'application/pdf' })

afterEach(async () => {
  for (const e of await auflisten()) await entfernen(e.id)
})

describe('Warteschlange in der IndexedDB', () => {
  it('speichert, listet, aktualisiert und entfernt Einträge', async () => {
    const a = await ablegen(pdf('a.pdf'), 'dateien')
    const b = await ablegen(pdf('b.pdf'), 'geteilt')
    expect(a).toMatchObject({ name: 'a.pdf', typ: 'application/pdf', herkunft: 'dateien', status: 'wartet' })

    const liste = await auflisten()
    expect(liste.map((e) => e.name).sort()).toEqual(['a.pdf', 'b.pdf'])
    expect((await lesen(b.id))?.herkunft).toBe('geteilt')

    await aktualisieren(a.id, { status: 'uebertragen', vorgangId: 'v-1' })
    expect(await lesen(a.id)).toMatchObject({ status: 'uebertragen', vorgangId: 'v-1' })

    await entfernen(a.id)
    expect((await auflisten()).map((e) => e.id)).toEqual([b.id])
  })

  it('übersteht ein Neuladen: eine neue Verbindung liest dieselbe Datenbank', async () => {
    const eintrag = await ablegen(pdf('nach-neustart.pdf'), 'dateien')
    await schliessen()
    const gelesen = await lesen(eintrag.id)
    expect(gelesen).toMatchObject({ name: 'nach-neustart.pdf', status: 'wartet', groesse: 8 })
    expect(gelesen?.blob).toBeDefined()
  })

  it('vergibt einen Eintrag nur einmal zum Senden', async () => {
    const eintrag = await ablegen(pdf('einmal.pdf'), 'dateien')
    const [erster, zweiter] = await Promise.all([beanspruchen(eintrag.id), beanspruchen(eintrag.id)])
    expect([erster?.status, zweiter?.status].filter(Boolean)).toEqual(['sendet'])
    // Eine abgebrochene Sendung wird nach zwei Minuten wieder frei.
    expect(await beanspruchen(eintrag.id, Date.now() + 3 * 60 * 1000)).toBeDefined()
  })

  it('entfernt übertragene Einträge nach 24 Stunden', async () => {
    const alt = await ablegen(pdf('alt.pdf'), 'dateien')
    const neu = await ablegen(pdf('neu.pdf'), 'dateien')
    const jetzt = Date.now()
    await aktualisieren(alt.id, { status: 'uebertragen', uebertragenAm: jetzt - sichtbarNachUebertragung - 1 })
    await aktualisieren(neu.id, { status: 'uebertragen', uebertragenAm: jetzt - 1000 })
    await aufraeumen(jetzt)
    expect((await auflisten()).map((e) => e.name)).toEqual(['neu.pdf'])
  })
})

describe('Senden aus dem Service Worker', () => {
  it('überträgt und merkt sich den Vorgang', async () => {
    const eintrag = await ablegen(pdf('ok.pdf'), 'geteilt')
    const f = vi.fn(async () => Response.json([{ id: 'v-7' }], { status: 202 }))
    await expect(sendenPerFetch((await beanspruchen(eintrag.id))!, f)).resolves.toBe('uebertragen')
    expect(f).toHaveBeenCalledWith('/api/import/upload', expect.objectContaining({ method: 'POST' }))
    expect(await lesen(eintrag.id)).toMatchObject({ status: 'uebertragen', vorgangId: 'v-7' })
  })

  it('lässt den Eintrag bei Netzwerkfehler und bei 401 wartend', async () => {
    const eintrag = await ablegen(pdf('offline.pdf'), 'dateien')
    const ohneNetz = vi.fn(async () => {
      throw new TypeError('Failed to fetch')
    })
    await expect(sendenPerFetch((await beanspruchen(eintrag.id))!, ohneNetz)).resolves.toBe('wartet')
    expect((await lesen(eintrag.id))?.status).toBe('wartet')

    const abgemeldet = vi.fn(async () => new Response(null, { status: 401 }))
    await expect(alleWartendenSenden(abgemeldet)).resolves.toEqual({ nichtAngemeldet: 1 })
    expect((await lesen(eintrag.id))?.status).toBe('wartet')
  })
})
