import { screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import type { BelegDatei } from '@/api/typen'
import { db } from '@/mocks/db'
import { b2, kopfGeladen, renderBelegDetail } from '../testHilfe'
import { dateiStatus, kurzHash } from './DateienTab'

vi.mock('react-pdf', () => import('../testHilfe').then((m) => m.reactPdfMock))

describe('Tab Dateien', () => {
  it('zeigt zwei Karten mit Status, Pfad und Aufbewahrungshinweis', async () => {
    renderBelegDetail(`/belege/${b2}/dateien`)
    await kopfGeladen()
    const karten = await screen.findAllByRole('article')
    expect(karten).toHaveLength(2)
    const original = screen.getByRole('article', { name: 'Original' })
    expect(original).toHaveTextContent('application/pdf')
    expect(original).toHaveTextContent('Prüfsumme ok')
    expect(original).toHaveTextContent('ablage/2026/Buerowelt_Kraemer_KG/2026-11-19_2026-48817.pdf')
    expect(original).toHaveTextContent(/geprüft 23\.11\.2026 03:00/)
    expect(within(original).getByRole('link', { name: /Herunterladen/ })).toHaveAttribute(
      'href',
      expect.stringContaining(`/api/belege/${b2}/dateien/`),
    )
    expect(screen.getByRole('article', { name: 'Vorschau' })).toHaveTextContent('abgeleitet')
    expect(screen.getByText(/^Aufbewahrung: Löschsperre bis/)).toHaveTextContent(
      'Löschsperre bis 31.12.2034 (8 Jahre nach Belegjahr 2026)',
    )
  })

  it('kopiert beim Klick den vollen Hash', async () => {
    const { nutzer } = renderBelegDetail(`/belege/${b2}/dateien`)
    await kopfGeladen()
    const sha = db.belege.find((b) => b.id === b2)!.dateien[0].sha256
    await nutzer.click(await screen.findByRole('button', { name: `SHA-256 ${kurzHash(sha)} kopieren` }))
    expect(await screen.findByText('Prüfsumme kopiert')).toBeInTheDocument()
    expect(await navigator.clipboard.readText()).toBe(sha)
  })

  it('leitet die Status-Chips ab', () => {
    const d = (teil: Partial<BelegDatei>) => ({ rolle: 'Original', pruefsummeOk: null, ...teil }) as BelegDatei
    expect(dateiStatus(d({ pruefsummeOk: true }))).toEqual({ text: 'Prüfsumme ok', ton: 'gruen' })
    expect(dateiStatus(d({ pruefsummeOk: false }))).toEqual({ text: 'Abweichung', ton: 'rot' })
    expect(dateiStatus(d({}))).toEqual({ text: 'noch nicht geprüft', ton: 'grau' })
    expect(dateiStatus(d({ rolle: 'EingebettetesXml' }))).toEqual({ text: 'abgeleitet', ton: 'grau' })
    expect(kurzHash('9f2c41d7' + '0'.repeat(48) + 'a83be41a')).toBe('9f2c41d7…a83be41a')
  })
})
