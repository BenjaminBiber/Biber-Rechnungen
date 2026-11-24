import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiFehler } from '@/api/client'
import { dateiHochladen } from '@/api/import'
import type { ImportVorgang } from '@/api/typen'
import { importVorgaenge } from '@/mocks/fixtures/import'
import * as ablage from '@/pwa/warteschlange'
import { ablehnungsgrund, formatGroesse, grundFormat, grundGroesse } from './pruefung'
import { maxGleichzeitig, warteschlange } from './uploadWarteschlange'

type Offen = { erfuellen: (v: ImportVorgang[]) => void; ablehnen: (f: unknown) => void }
const offen: Offen[] = []

vi.mock('@/api/import', async (original) => ({
  ...(await original<typeof import('@/api/import')>()),
  dateiHochladen: vi.fn(
    (_datei: File, fortschritt: (p: number) => void) =>
      new Promise<ImportVorgang[]>((erfuellen, ablehnen) => {
        fortschritt(10)
        offen.push({ erfuellen, ablehnen })
      }),
  ),
}))

const pdf = (name: string) => new File(['%PDF'], name, { type: 'application/pdf' })
const tick = () => new Promise((r) => setTimeout(r, 0))

/** Simuliert `navigator.onLine` samt Ereignis. */
function netz(online: boolean) {
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(online)
  window.dispatchEvent(new Event(online ? 'online' : 'offline'))
}

afterEach(async () => {
  await warteschlange.zuruecksetzen()
  offen.length = 0
  vi.mocked(dateiHochladen).mockClear()
})

describe('Prüfung vor dem Senden', () => {
  it('lehnt fremde Formate und Dateien über 50 MB ab', () => {
    expect(ablehnungsgrund({ name: 'foto.heic', size: 10 })).toBe(grundFormat)
    expect(ablehnungsgrund({ name: 'gross.pdf', size: 51 * 1024 * 1024 })).toBe(grundGroesse)
    expect(ablehnungsgrund({ name: 'Scan.TIFF', size: 10 })).toBeNull()
    expect(formatGroesse(284 * 1024)).toBe('284 KB')
    expect(formatGroesse(2.1 * 1024 * 1024)).toBe('2,1 MB')
  })
})

describe('Upload-Warteschlange', () => {
  it('lädt höchstens drei Dateien gleichzeitig, die nächste startet nach einer fertigen', async () => {
    warteschlange.hinzufuegen([1, 2, 3, 4, 5].map((n) => pdf(`r${n}.pdf`)))
    expect(warteschlange.stand().every((e) => e.status === 'laedt')).toBe(true)
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledTimes(maxGleichzeitig))
    await tick()
    expect(dateiHochladen).toHaveBeenCalledTimes(maxGleichzeitig)

    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledTimes(4))
    const erster = vi.mocked(dateiHochladen).mock.calls[0][0].name
    expect(warteschlange.stand().find((e) => e.name === erster)).toMatchObject({
      status: 'verarbeitung',
      prozent: 100,
    })
  })

  it('schreibt jede Datei zuerst in die IndexedDB', async () => {
    warteschlange.hinzufuegen([pdf('gespeichert.pdf')])
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledOnce())
    const gespeichert = await ablage.auflisten()
    expect(gespeichert).toHaveLength(1)
    expect(gespeichert[0]).toMatchObject({ name: 'gespeichert.pdf', herkunft: 'dateien', status: 'sendet' })

    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await vi.waitFor(async () => expect((await ablage.auflisten())[0].status).toBe('uebertragen'))
  })

  it('sendet abgelehnte Dateien nicht', async () => {
    warteschlange.hinzufuegen([new File(['x'], 'foto_beleg.heic')])
    await tick()
    expect(dateiHochladen).not.toHaveBeenCalled()
    expect(warteschlange.stand()[0]).toMatchObject({ status: 'abgelehnt', meldung: grundFormat })
  })

  it('lässt Einträge bei Netzwerkfehler wartend; „erneut“ lädt wieder hoch', async () => {
    warteschlange.hinzufuegen([pdf('a.pdf')])
    await vi.waitFor(() => expect(offen).toHaveLength(1))
    offen[0].ablehnen(new ApiFehler(0, 'Netzwerkfehler beim Hochladen'))
    await vi.waitFor(() => expect(warteschlange.stand()[0].status).toBe('wartet'))
    expect((await ablage.auflisten())[0].status).toBe('wartet')

    warteschlange.erneut(warteschlange.stand()[0].id)
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledTimes(2))
    expect(warteschlange.stand()[0].status).toBe('laedt')
  })

  it('offline bleibt der Eintrag „wartet“, nach „online“ wird gesendet', async () => {
    warteschlange.starten()
    netz(false)
    warteschlange.hinzufuegen([pdf('offline.pdf')])
    expect(warteschlange.stand()[0].status).toBe('wartet')
    await vi.waitFor(async () => expect(await ablage.auflisten()).toHaveLength(1))
    await tick()
    expect(dateiHochladen).not.toHaveBeenCalled()
    expect(warteschlange.stand()[0].status).toBe('wartet')

    netz(true)
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledOnce())
    expect(warteschlange.stand()[0].status).toBe('laedt')
  })

  it('401 lässt den Eintrag stehen und verlangt eine Anmeldung', async () => {
    warteschlange.hinzufuegen([pdf('abgemeldet.pdf')])
    await vi.waitFor(() => expect(offen).toHaveLength(1))
    offen[0].ablehnen(new ApiFehler(401, 'Nicht angemeldet'))
    await vi.waitFor(() => expect(warteschlange.stand()[0].status).toBe('wartet'))
    expect(warteschlange.anmeldungNoetig()).toBe(true)
    expect((await ablage.auflisten())[0].status).toBe('wartet')
  })

  it('übersteht einen Neustart: wartende Einträge aus der IndexedDB werden nachgesendet', async () => {
    const gespeichert = await ablage.ablegen(pdf('vor-neustart.pdf'), 'geteilt')
    await ablage.schliessen()
    expect(warteschlange.stand()).toHaveLength(0)

    await warteschlange.nachsenden()
    expect(warteschlange.stand()[0]).toMatchObject({
      id: gespeichert.id,
      name: 'vor-neustart.pdf',
      herkunft: 'geteilt',
    })
    await vi.waitFor(() => expect(dateiHochladen).toHaveBeenCalledOnce())
  })

  it('übernimmt Endzustände des Vorgangs', async () => {
    warteschlange.hinzufuegen([pdf('b.pdf')])
    await vi.waitFor(() => expect(offen).toHaveLength(1))
    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await vi.waitFor(() => expect(warteschlange.stand()[0].status).toBe('verarbeitung'))
    const id = warteschlange.stand()[0].id
    warteschlange.vorgangAktualisiert(id, { ...importVorgaenge[0], status: 'Dublette' })
    expect(warteschlange.stand()[0].status).toBe('dublette')
  })

  it('setzt einen laufenden Eintrag mit unbekanntem Vorgang auf „fehler“', async () => {
    warteschlange.hinzufuegen([pdf('c.pdf')])
    await vi.waitFor(() => expect(offen).toHaveLength(1))
    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await vi.waitFor(() => expect(warteschlange.stand()[0].status).toBe('verarbeitung'))
    const id = warteschlange.stand()[0].id
    warteschlange.vorgangUnbekannt(id)
    expect(warteschlange.stand()[0]).toMatchObject({
      status: 'fehler',
      meldung: 'Import-Vorgang nicht mehr vorhanden.',
    })
    expect((await ablage.lesen(id))?.status).toBe('fehler')
  })
})
