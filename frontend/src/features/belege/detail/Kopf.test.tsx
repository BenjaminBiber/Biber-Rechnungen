import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { belegIds } from '@/mocks/fixtures/ids'
import { szenario } from '@/mocks/szenarien'
import { b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

describe('Beleg-Detail: Rahmen und Kopf', () => {
  it('zeigt die Daten des Design-Belegs', async () => {
    renderBelegDetail()
    await kopfGeladen()
    const kopf = screen.getByRole('heading', { level: 1 }).parentElement!.parentElement!
    expect(kopf).toHaveTextContent('Rechnung')
    expect(kopf).toHaveTextContent('Inbox')
    expect(kopf).toHaveTextContent('zu prüfen')
    expect(kopf).not.toHaveTextContent('Nr. 2026-48817')
    expect(kopf).not.toHaveTextContent('264,00 €')
    expect(screen.getByRole('link', { name: 'Zurück zu Belege' })).toHaveAttribute('href', '/belege')
    expect(screen.queryByRole('navigation', { name: 'Blättern' })).not.toBeInTheDocument()
  })

  it('zeigt für Benutzer bei „zu prüfen“ alle Aktionen außer „Aufteilen“ (nur eine Seite)', async () => {
    renderBelegDetail()
    await kopfGeladen()
    const aktionen = screen.getByRole('group', { name: 'Beleg-Aktionen' })
    expect(within(aktionen).getByRole('link', { name: 'Original herunterladen' })).toHaveAttribute(
      'href',
      expect.stringContaining(`/api/belege/${b2}/dateien/`),
    )
    expect(within(aktionen).getByRole('link', { name: 'Original herunterladen' })).toHaveAttribute('download')
    expect(within(aktionen).getByRole('button', { name: 'Weitere Aktionen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Erneut auslesen' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bestätigen' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Aufteilen' })).not.toBeInTheDocument()
  })

  it('zeigt „Aufteilen“ bei mehrseitigem PDF und kein „Bestätigen“ ohne „zu prüfen“', async () => {
    renderBelegDetail(`/belege/${belegIds.b3}`)
    await kopfGeladen('Autohaus Brenner GmbH')
    expect(screen.getByRole('link', { name: 'Aufteilen' })).toHaveAttribute('href', `/belege/${belegIds.b3}/aufteilen`)
    expect(screen.queryByRole('button', { name: 'Bestätigen' })).not.toBeInTheDocument()
  })

  it('zeigt NurLesen nur „Mehr“', async () => {
    szenario.rolle('NurLesen')
    renderBelegDetail()
    await kopfGeladen()
    expect(screen.getByRole('button', { name: 'Weitere Aktionen' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Erneut auslesen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Bestätigen' })).not.toBeInTheDocument()
  })

  it('stößt „Erneut auslesen“ über das Aktionsmenü an', async () => {
    const { nutzer } = renderBelegDetail()
    await kopfGeladen()
    await nutzer.click(screen.getByRole('button', { name: 'Weitere Aktionen' }))
    await nutzer.click(await screen.findByRole('menuitem', { name: /Erneut auslesen/ }))
    expect(await screen.findByText('Beleg wird erneut ausgelesen')).toBeInTheDocument()
  })

  it('blättert mit den Filtern aus der Liste („aus“)', async () => {
    const aus = encodeURIComponent(JSON.stringify({ ansicht: 'Inbox' }))
    renderBelegDetail(`/belege/${b2}?aus=${aus}`)
    await kopfGeladen()
    expect(screen.getByRole('link', { name: 'Zurück zu Inbox' }).getAttribute('href')).toContain('ansicht=Inbox')
    const blaettern = await screen.findByRole('navigation', { name: 'Blättern' })
    expect(blaettern).toHaveTextContent('2 von 4 in Inbox')
    expect(within(blaettern).getByRole('link', { name: 'Vorheriger Beleg' }).getAttribute('href')).toContain(
      `/belege/${belegIds.b1}`,
    )
    expect(within(blaettern).getByRole('link', { name: 'Nächster Beleg' }).getAttribute('href')).toContain(
      `/belege/${belegIds.b5}`,
    )
  })

  it('meldet einen unbekannten Beleg', async () => {
    renderBelegDetail('/belege/00000000-0000-4000-8000-000000000000')
    expect(
      await screen.findByRole('heading', { level: 1, name: 'Beleg nicht gefunden' }, { timeout: 5000 }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Zur Belegliste' })).toHaveAttribute('href', '/belege')
  })

  it('verlinkt alle Tabs mit Zählern', async () => {
    const { nutzer, router } = renderBelegDetail()
    await kopfGeladen()
    const tabs = screen.getByRole('navigation', { name: 'Belegdaten' })
    const links = within(tabs).getAllByRole('link')
    expect(links.map((l) => l.textContent)).toEqual([
      'Daten',
      'Positionen6',
      'Prüfung2 fehlgeschlagen',
      'Historie',
      'Dateien2',
      'Dubletten',
    ])
    expect(within(tabs).getByRole('link', { name: 'Daten' })).toHaveAttribute('aria-current', 'page')
    await nutzer.click(within(tabs).getByRole('link', { name: /^Prüfung/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}/pruefung`))
    expect(within(tabs).getByRole('link', { name: /^Prüfung/ })).toHaveAttribute('aria-current', 'page')
  })
})
