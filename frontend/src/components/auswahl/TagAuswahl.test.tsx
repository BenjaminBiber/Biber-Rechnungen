import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { tagIds } from '@/mocks/fixtures/ids'
import { TagAuswahl } from './TagAuswahl'
import { renderAuswahl } from './testHilfe'

function oeffnen(props: Partial<Parameters<typeof TagAuswahl>[0]> = {}) {
  const onAendern = vi.fn()
  const ergebnis = renderAuswahl(
    <TagAuswahl gewaehlt={[]} onAendern={onAendern} ausloeser={<button type="button">Tags</button>} {...props} />,
  )
  return { ...ergebnis, onAendern }
}

describe('TagAuswahl', () => {
  it('wählt einen Tag per Klick an und wieder ab', async () => {
    const { nutzer, onAendern } = oeffnen({ gewaehlt: [tagIds.bez] })
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await nutzer.click(await screen.findByRole('option', { name: /^bezahlt PayPal/ }))
    expect(onAendern).toHaveBeenLastCalledWith([tagIds.bez, tagIds.pp])
    await nutzer.click(screen.getByRole('option', { name: /^bezahlt\s*Zahlung/ }))
    expect(onAendern).toHaveBeenLastCalledWith([])
  })

  it('filtert über die Suche und zeigt die Hinweise „Zahlung“ und „System“', async () => {
    const { nutzer } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    expect(screen.getByRole('option', { name: /zu prüfen/ })).toHaveTextContent('System')
    await nutzer.type(screen.getByRole('combobox', { name: 'Tag suchen' }), 'bez')
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual([
      expect.stringContaining('bezahltZahlung'),
      expect.stringContaining('bezahlt PayPalZahlung'),
    ])
  })

  it('der Systemtag „zu prüfen“ ist nicht wählbar', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    const system = await screen.findByRole('option', { name: /zu prüfen/ })
    expect(system).toHaveAttribute('aria-disabled', 'true')
    await nutzer.click(system)
    expect(onAendern).not.toHaveBeenCalled()
  })

  it('nurZahlungsTags zeigt nur Zahlungs-Tags', async () => {
    const { nutzer } = oeffnen({ nurZahlungsTags: true })
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Lastschrift/ })
    expect(screen.getAllByRole('option')).toHaveLength(3)
  })

  it('Tastatur: Pfeiltasten überspringen den Systemtag, Enter wählt', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    await nutzer.keyboard('{ArrowDown}{ArrowDown}{Enter}')
    expect(onAendern).toHaveBeenLastCalledWith([tagIds.bez])
    await nutzer.keyboard('{Escape}')
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('legt einen neuen Tag an und wählt ihn', async () => {
    const { nutzer, onAendern } = oeffnen({ neuAnlegenErlaubt: true })
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    await nutzer.type(screen.getByRole('combobox'), 'Bewirtung')
    await nutzer.click(screen.getByRole('option', { name: /„Bewirtung“ als neuen Tag anlegen/ }))
    await waitFor(() => expect(onAendern).toHaveBeenCalledWith([expect.any(String)]))
  })

  it('Enter ohne Markierung legt den eingetippten Namen als neuen Tag an', async () => {
    const { nutzer, onAendern } = oeffnen({ neuAnlegenErlaubt: true, gewaehlt: [tagIds.bez] })
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    await nutzer.type(screen.getByRole('combobox'), 'Bewirtung{Enter}')
    await waitFor(() => expect(onAendern).toHaveBeenCalledWith([tagIds.bez, expect.any(String)]))
    expect(screen.getByRole('combobox')).toHaveValue('')
  })

  it('Enter legt nichts an, wenn der Name schon existiert oder Anlegen nicht erlaubt ist', async () => {
    const { nutzer, onAendern } = oeffnen({ neuAnlegenErlaubt: true })
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    await nutzer.type(screen.getByRole('combobox'), 'inbox{Enter}')
    expect(screen.queryByRole('option', { name: /als neuen Tag anlegen/ })).not.toBeInTheDocument()
    expect(onAendern).not.toHaveBeenCalled()
  })

  it('ohne neuAnlegenErlaubt gibt es keine Anlage-Option', async () => {
    const { nutzer, onAendern } = oeffnen()
    await nutzer.click(screen.getByRole('button', { name: 'Tags' }))
    await screen.findByRole('option', { name: /Inbox/ })
    await nutzer.type(screen.getByRole('combobox'), 'Bewirtung{Enter}')
    expect(screen.queryByRole('option')).not.toBeInTheDocument()
    expect(onAendern).not.toHaveBeenCalled()
  })
})
