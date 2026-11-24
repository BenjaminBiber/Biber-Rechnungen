import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiFehler } from '@/api/client'
import { dateiHochladen } from '@/api/import'
import type { ImportVorgang } from '@/api/typen'
import { importVorgaenge } from '@/mocks/fixtures/import'
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

afterEach(() => {
  warteschlange.zuruecksetzen()
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
    expect(dateiHochladen).toHaveBeenCalledTimes(maxGleichzeitig)
    expect(warteschlange.stand().every((e) => e.status === 'laedt')).toBe(true)

    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await tick()
    expect(dateiHochladen).toHaveBeenCalledTimes(4)
    expect(warteschlange.stand().find((e) => e.name === 'r1.pdf')).toMatchObject({
      status: 'verarbeitung',
      prozent: 100,
    })
  })

  it('sendet abgelehnte Dateien nicht', () => {
    warteschlange.hinzufuegen([new File(['x'], 'foto_beleg.heic')])
    expect(dateiHochladen).not.toHaveBeenCalled()
    expect(warteschlange.stand()[0]).toMatchObject({ status: 'abgelehnt', meldung: grundFormat })
  })

  it('markiert Netzwerkfehler als wiederholbar und lädt bei „erneut“ wieder hoch', async () => {
    warteschlange.hinzufuegen([pdf('a.pdf')])
    offen[0].ablehnen(new ApiFehler(0, 'Netzwerkfehler beim Hochladen'))
    await tick()
    const eintrag = warteschlange.stand()[0]
    expect(eintrag).toMatchObject({ status: 'fehler', wiederholbar: true })

    warteschlange.erneut(eintrag.id)
    expect(dateiHochladen).toHaveBeenCalledTimes(2)
    expect(warteschlange.stand()[0].status).toBe('laedt')
  })

  it('übernimmt Endzustände des Vorgangs', async () => {
    warteschlange.hinzufuegen([pdf('b.pdf')])
    offen[0].erfuellen([{ ...importVorgaenge[0], status: 'Angenommen' }])
    await tick()
    const id = warteschlange.stand()[0].id
    warteschlange.vorgangAktualisiert(id, { ...importVorgaenge[0], status: 'Dublette' })
    expect(warteschlange.stand()[0].status).toBe('dublette')
  })
})
