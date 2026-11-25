import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/mocks/server'
import { renderMitApp } from '@/test/render'
import { Seitenleiste } from './Seitenleiste'

describe('Seitenleiste', () => {
  it('zeigt alle acht Einträge mit ihren Zielen', async () => {
    renderMitApp(<Seitenleiste />)
    const nav = await screen.findByRole('navigation', { name: 'Hauptnavigation' })
    const erwartet: Array<[string, string]> = [
      ['Dashboard', '/'],
      ['Belege', '/belege'],
      ['Hochladen', '/hochladen'],
      ['Korrespondenten', '/korrespondenten'],
      ['Artikel', '/artikel'],
      ['Tags & Kategorien', '/tags'],
      ['Export', '/export'],
      ['Einstellungen', '/einstellungen'],
    ]
    for (const [name, ziel] of erwartet) {
      expect(within(nav).getByRole('link', { name: new RegExp(`^${name}`) })).toHaveAttribute('href', ziel)
    }
  })

  it('zeigt den Inbox-Zähler 12 an „Belege“', async () => {
    server.use(
      http.get('*/api/belege/zaehler', () =>
        HttpResponse.json({ alle: 40, inbox: 12, zuPruefen: 3, offen: 5, ueberfaellig: 1 }),
      ),
    )
    renderMitApp(<Seitenleiste />)
    const badge = await screen.findByLabelText(/in der Inbox$/)
    expect(badge).toHaveTextContent('12')
  })

  it('klappt auf Icons ein, zeigt dann Tooltips und merkt sich den Zustand', async () => {
    localStorage.removeItem('seitenleiste-eingeklappt')
    const user = userEvent.setup()
    renderMitApp(<Seitenleiste />)
    const knopf = await screen.findByRole('button', { name: 'Seitenleiste einklappen' })
    expect(knopf).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('link', { name: 'Export' })).not.toHaveAttribute('title')

    await user.click(knopf)
    expect(screen.getByRole('button', { name: 'Seitenleiste ausklappen' })).toHaveAttribute('aria-expanded', 'false')
    expect(screen.getByRole('link', { name: 'Export' })).toHaveAttribute('title', 'Export')
    expect(localStorage.getItem('seitenleiste-eingeklappt')).toBe('true')

    await user.click(screen.getByRole('button', { name: 'Seitenleiste ausklappen' }))
    expect(localStorage.getItem('seitenleiste-eingeklappt')).toBe('false')
  })

  it('markiert den aktiven Eintrag über den Router', async () => {
    renderMitApp(<Seitenleiste />, { route: '/belege' })
    const belege = await screen.findByRole('link', { name: /^Belege/ })
    expect(belege).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Dashboard' })).not.toHaveAttribute('aria-current')
  })
})
