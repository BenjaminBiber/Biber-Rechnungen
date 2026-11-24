import { act, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { dateiHochladen } from '@/api/import'
import type { ImportVorgang } from '@/api/typen'
import { warteschlange, type UploadEintrag } from '@/features/hochladen/uploadWarteschlange'
import { renderMitApp } from '@/test/render'
import { MobilHochladen, notiz, zaehlerText } from './MobilHochladen'

// Hochladen bleibt hängen – so lässt sich der Zustand „sendet“ beobachten.
vi.mock('@/api/import', async (original) => ({
  ...(await original<typeof import('@/api/import')>()),
  dateiHochladen: vi.fn(
    (_datei: File, fortschritt: (p: number) => void) =>
      new Promise<ImportVorgang[]>(() => {
        fortschritt(68)
      }),
  ),
}))

const pdf = (name: string, groesse = 1024) => {
  const datei = new File(['%PDF'], name, { type: 'application/pdf' })
  Object.defineProperty(datei, 'size', { value: groesse })
  return datei
}

function netz(online: boolean) {
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(online)
  act(() => {
    window.dispatchEvent(new Event(online ? 'online' : 'offline'))
  })
}

const banner = 'Offline – Uploads werden gesendet, sobald du wieder im LAN bist.'

afterEach(async () => {
  await warteschlange.zuruecksetzen()
  vi.mocked(dateiHochladen).mockClear()
})

describe('Mobil Hochladen', () => {
  it('zeigt offline das Banner und „3 warten“', async () => {
    netz(false)
    renderMitApp(<MobilHochladen />)
    const eingabe = await screen.findByLabelText('Dateien auswählen')
    expect(screen.getByRole('status')).toHaveTextContent(banner)

    await userEvent.upload(eingabe, [
      pdf('Rechnung_Hostwerk_11.pdf', 284 * 1024),
      pdf('Parkhaus_Quittung.pdf', 96 * 1024),
      pdf('Rechnung_Weller.pdf', 41 * 1024),
    ])
    const karte = within(screen.getByRole('region', { name: 'Warteschlange' }))
    expect(karte.getByText('3 warten')).toBeInTheDocument()
    expect(karte.getAllByRole('img', { name: 'wartet' })).toHaveLength(3)
    expect(karte.getByText('aus Dateien · 284 KB')).toBeInTheDocument()
    expect(dateiHochladen).not.toHaveBeenCalled()
  })

  it('zeigt online kein Banner und „1 sendet“ mit Fortschritt', async () => {
    netz(true)
    renderMitApp(<MobilHochladen />)
    const eingabe = await screen.findByLabelText('Dateien auswählen')
    expect(screen.queryByText(banner)).not.toBeInTheDocument()
    await userEvent.upload(eingabe, [pdf('xrechnung_1194.xml', 41 * 1024)])
    const karte = within(await screen.findByRole('region', { name: 'Warteschlange' }))
    // Erst IndexedDB, dann Senden – unter Last braucht das etwas länger.
    expect(await karte.findByText('1 sendet', undefined, { timeout: 5000 })).toBeInTheDocument()
    expect(await karte.findByText('68 %', undefined, { timeout: 5000 })).toBeInTheDocument()
    expect(karte.getByRole('img', { name: 'sendet' })).toBeInTheDocument()
  }, 15000)

  it('das Datei-Feld hat kein capture-Attribut (kein Kamera-Scan)', async () => {
    renderMitApp(<MobilHochladen />)
    const eingabe = await screen.findByLabelText('Dateien auswählen')
    expect(eingabe).toHaveAttribute('type', 'file')
    expect(eingabe).toHaveAttribute('multiple')
    expect(eingabe).not.toHaveAttribute('capture')
  })
})

describe('Texte der Warteschlange', () => {
  const basis: UploadEintrag = { id: '1', name: 'a.pdf', groesse: 284 * 1024, status: 'wartet', prozent: 0 }

  it('zählt wartende vor sendenden', () => {
    expect(zaehlerText(3, 1)).toBe('3 warten')
    expect(zaehlerText(1, 0)).toBe('1 wartet')
    expect(zaehlerText(0, 1)).toBe('1 sendet')
    expect(zaehlerText(0, 0)).toBe('')
  })

  it('beschreibt Herkunft, Fortschritt und Ergebnis', () => {
    expect(notiz({ ...basis, herkunft: 'geteilt' })).toBe('geteilt · 284 KB')
    expect(notiz({ ...basis, herkunft: 'dateien', groesse: 96 * 1024 })).toBe('aus Dateien · 96 KB')
    expect(notiz({ ...basis, status: 'laedt', prozent: 68 })).toBe('68 %')
    expect(notiz({ ...basis, status: 'verarbeitung' })).toBe('in Verarbeitung')
    const gestern = Date.now() - 24 * 60 * 60 * 1000
    expect(
      notiz({
        ...basis,
        status: 'fertig',
        uebertragenAm: gestern,
        vorgang: { belegId: 'b-1' } as ImportVorgang,
      }),
    ).toBe('gestern · Beleg angelegt')
  })
})
