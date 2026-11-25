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

  it('zeigt einen Punkt ohne Zahl an „Belege“, wenn Belege zur Prüfung sind', async () => {
    server.use(
      http.get('*/api/belege/zaehler', () =>
        HttpResponse.json({ alle: 40, inbox: 12, zuPruefen: 3, offen: 5, ueberfaellig: 1 }),
      ),
    )
    renderMitApp(<Seitenleiste />)
    const belege = await screen.findByRole('link', { name: 'Belege, Belege zur Prüfung' })
    expect(within(belege).getByTestId('belege-hinweis')).toBeEmptyDOMElement()
    expect(belege).not.toHaveTextContent(/\d/)
  })

  it('zeigt keinen Punkt, wenn nichts zur Prüfung ist', async () => {
    server.use(
      http.get('*/api/belege/zaehler', () =>
        HttpResponse.json({ alle: 40, inbox: 12, zuPruefen: 0, offen: 5, ueberfaellig: 1 }),
      ),
    )
    renderMitApp(<Seitenleiste />)
    await screen.findByRole('link', { name: 'Belege' })
    expect(screen.queryByTestId('belege-hinweis')).not.toBeInTheDocument()
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
