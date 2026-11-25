import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { belegIds, korrespondentIds } from '@/mocks/fixtures/ids'
import { szenario } from '@/mocks/szenarien'
import { b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

async function oeffneMenue(pfad?: string, name?: string) {
  const ergebnis = renderBelegDetail(pfad)
  await kopfGeladen(name)
  await ergebnis.nutzer.click(screen.getByRole('button', { name: 'Weitere Aktionen' }))
  const menue = await screen.findByRole('menu')
  return { ...ergebnis, menue }
}

const eintraege = () => screen.getAllByRole('menuitem').map((e) => e.textContent)

describe('Mehr-Menü', () => {
  it('zeigt bei einem gescannten Beleg alle Einträge außer „XML herunterladen“ (Herunterladen ist die Hauptaktion)', async () => {
    await oeffneMenue()
    expect(eintraege()).toEqual([
      'refreshErneut auslesen',
      'folder_copyAblagepfad kopieren',
      'linkLink kopieren',
      'content_copyAls Dublette markieren …',
      'cropLogo aus diesem Beleg übernehmen',
      'swap_horizBelegart ändern',
      'lockLöschengesperrt bis 31.12.2034',
    ])
    expect(screen.queryByRole('menuitem', { name: /Original herunterladen/ })).not.toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /Löschen/ })).toHaveAttribute('aria-disabled', 'true')
  })

  it('„Erneut auslesen“ stößt das Auslesen an', async () => {
    const { nutzer } = await oeffneMenue()
    await nutzer.click(screen.getByRole('menuitem', { name: /Erneut auslesen/ }))
    expect(await screen.findByText('Beleg wird erneut ausgelesen')).toBeInTheDocument()
  })

  it('bietet bei E-Rechnungen „XML herunterladen“ an', async () => {
    await oeffneMenue(`/belege/${belegIds.b1}`, 'Hostwerk Süd GmbH')
    const xml = screen.getByRole('menuitem', { name: /XML herunterladen/ })
    expect(xml).toHaveTextContent('E-Rechnung')
    expect(xml.getAttribute('href')).toMatch(/\/dateien\/da0+-0000-4000-8000-0+12$/)
  })

  it('„Link kopieren“ schreibt in die Zwischenablage', async () => {
    const { nutzer } = await oeffneMenue()
    await nutzer.click(screen.getByRole('menuitem', { name: /Link kopieren/ }))
    expect(await screen.findByText('Kopiert')).toBeInTheDocument()
    expect(await navigator.clipboard.readText()).toBe(`${window.location.origin}/belege/${b2}`)
  })

  it('„Ablagepfad kopieren“ kopiert den Pfad des Originals', async () => {
    const { nutzer } = await oeffneMenue()
    await nutzer.click(screen.getByRole('menuitem', { name: /Ablagepfad kopieren/ }))
    expect(await screen.findByText('Kopiert')).toBeInTheDocument()
    expect(await navigator.clipboard.readText()).toBe('ablage/2026/Buerowelt_Kraemer_KG/2026-11-19_2026-48817.pdf')
  })

  it('„Als Dublette markieren …“ öffnet den Tab Dubletten', async () => {
    const { nutzer, router } = await oeffneMenue()
    await nutzer.click(screen.getByRole('menuitem', { name: /Als Dublette markieren/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}/dubletten`))
    expect(router.state.location.search).toMatchObject({ markieren: 1 })
  })

  it('„Logo aus diesem Beleg übernehmen“ führt zum Korrespondenten', async () => {
    const { nutzer, router } = await oeffneMenue()
    await nutzer.click(screen.getByRole('menuitem', { name: /Logo aus diesem Beleg/ }))
    await waitFor(() =>
      expect(router.state.location.pathname).toBe(`/korrespondenten/${korrespondentIds.bueroweltKraemer}`),
    )
    expect(router.state.location.search).toMatchObject({ logoAus: b2 })
  })

  it('„Belegart ändern“ setzt den Fokus auf das Feld im Tab „Daten“', async () => {
    const { nutzer } = await oeffneMenue(`/belege/${b2}/historie`)
    await nutzer.click(screen.getByRole('menuitem', { name: /Belegart ändern/ }))
    await waitFor(() => expect(screen.getByLabelText('Belegart')).toHaveFocus())
  })

  it('zeigt NurLesen keine ändernden Einträge', async () => {
    szenario.rolle('NurLesen')
    await oeffneMenue()
    expect(eintraege()).toEqual([
      'folder_copyAblagepfad kopieren',
      'linkLink kopieren',
      'lockLöschengesperrt bis 31.12.2034',
    ])
    // Bei offenem Menü ist der Rest der Seite aria-hidden.
    expect(screen.getByRole('link', { name: 'Original herunterladen', hidden: true })).toHaveAttribute('download')
  })
})
