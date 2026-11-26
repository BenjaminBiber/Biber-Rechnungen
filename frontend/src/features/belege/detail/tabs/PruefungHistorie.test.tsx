import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { db } from '@/mocks/db'
import { b2, kopfGeladen, renderBelegDetail } from '../testHilfe'

vi.mock('react-pdf', () => import('../testHilfe').then((m) => m.reactPdfMock))

describe('Tab Prüfung', () => {
  it('zeigt zehn Befunde: fehlgeschlagen → ok → nicht anwendbar', async () => {
    renderBelegDetail(`/belege/${b2}/pruefung`)
    await kopfGeladen()
    const eintraege = within(await screen.findByRole('list')).getAllByRole('listitem')
    expect(eintraege).toHaveLength(10)
    const ergebnisse = eintraege.map((e) => e.querySelector('.chip')?.textContent)
    expect(ergebnisse).toEqual([
      'fehlgeschlagen',
      'fehlgeschlagen',
      'ok',
      'ok',
      'ok',
      'ok',
      'ok',
      'ok',
      'ok',
      'nicht anwendbar',
    ])
    expect(eintraege[0]).toHaveTextContent('Summe der Positionen = Netto')
    expect(eintraege[0].querySelector('[data-icon="report"]')).toBeInTheDocument()
    expect(eintraege[9].querySelector('[data-icon="remove_circle_outline"]')).toBeInTheDocument()
  })

  it('zeigt ohne Befunde „Noch nicht geprüft“', async () => {
    db.belege = db.belege.map((b) => (b.id === b2 ? { ...b, befunde: [] } : b))
    renderBelegDetail(`/belege/${b2}/pruefung`)
    await kopfGeladen()
    expect(await screen.findByText('Noch nicht geprüft')).toBeInTheDocument()
  })
})

describe('Tab Historie', () => {
  it('zeigt acht Einträge, die Änderung „Sonstiges → Bürobedarf“ zuerst', async () => {
    renderBelegDetail(`/belege/${b2}/historie`)
    await kopfGeladen()
    expect(screen.getByText('Lückenloses Änderungsprotokoll · nicht änderbar oder löschbar')).toBeInTheDocument()
    const liste = await screen.findByRole('list')
    const eintraege = await within(liste).findAllByRole('listitem')
    expect(eintraege).toHaveLength(8)
    expect(eintraege[0]).toHaveTextContent('A. Lehmann · 20.11.2026, 09:02')
    expect(eintraege[0]).toHaveTextContent('Kategorie geändert')
    expect(eintraege[0]).toHaveTextContent('Sonstiges → Bürobedarf')
    expect(within(eintraege[0]).getByText('Sonstiges').tagName).toBe('DEL')
    expect(eintraege[3]).toHaveTextContent('Tags gesetzt: Inbox, zu prüfen (2 Befunde)')
    expect(eintraege[3].querySelector('.bg-orange-flaeche')).not.toBeNull()
    expect(eintraege[7]).toHaveTextContent('Importiert aus Eingangsordner: Scan_20261119_0812.pdf')
  })
})
