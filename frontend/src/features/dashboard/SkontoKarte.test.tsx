import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { SkontoEintrag } from '@/api/typen'
import { renderMitApp } from '@/test/render'
import { SkontoKarte } from './SkontoKarte'

const heute = new Date(2026, 10, 24)

function eintrag(belegId: string, bis: string, betrag: number | null = 2): SkontoEintrag {
  return { belegId, korrespondent: `Firma ${belegId}`, rechnungsnummer: null, brutto: 100, prozent: 2, bis, betrag }
}

describe('SkontoKarte', () => {
  it('nennt heute, morgen und spätere Fristen; bis morgen ist orange', async () => {
    renderMitApp(
      <SkontoKarte
        heute={heute}
        skonto={{
          anzahl: 3,
          ersparnis: 4,
          eintraege: [eintrag('a', '2026-11-24'), eintrag('b', '2026-11-25'), eintrag('c', '2026-12-01', null)],
        }}
      />,
    )
    // jsdom löst CSS-Variablen nicht auf, deshalb der Blick ins style-Attribut.
    const farbe = (text: string) => screen.getByText(text).getAttribute('style')
    await screen.findByText('2 % nur noch heute')
    expect(farbe('2 % nur noch heute')).toContain('--color-orange-text')
    expect(farbe('2 % bis morgen')).toContain('--color-orange-text')
    expect(farbe('2 % bis 01.12.2026')).toContain('--color-gedaempft-stark')
    expect(screen.getByText('3 Rechnungen')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Firma c/ })).toHaveTextContent('–')
  })

  it('ohne laufende Frist: Hinweis statt Liste', async () => {
    renderMitApp(<SkontoKarte heute={heute} skonto={{ anzahl: 0, ersparnis: 0, eintraege: [] }} />)
    expect(await screen.findByText('Keine offenen Rechnungen mit laufender Skontofrist.')).toBeInTheDocument()
    expect(screen.getByText('0 Rechnungen')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })
})
