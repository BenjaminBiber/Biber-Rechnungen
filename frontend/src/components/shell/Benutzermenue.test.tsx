import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { renderMitApp } from '@/test/render'
import { Benutzermenue } from './Benutzermenue'

async function oeffnen() {
  const user = userEvent.setup()
  const ergebnis = renderMitApp(<Benutzermenue />)
  const ausloeser = await screen.findByRole('button', { name: 'Benutzermenü' })
  await screen.findByText('M. Keller')
  await user.click(ausloeser)
  await screen.findByText('Rolle: Admin · 2FA aktiv')
  return { user, ...ergebnis }
}

describe('Benutzermenü', () => {
  it('zeigt Name, Rolle, 2FA und die Konto-Links', async () => {
    await oeffnen()
    expect(screen.getByText('Martin Keller')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Mein Konto' })).toHaveAttribute('href', '/einstellungen/konto')
    expect(screen.getByRole('link', { name: 'Zwei-Faktor-Anmeldung' })).toHaveAttribute('href', '/einstellungen/konto')
    expect(screen.getByRole('link', { name: 'API-Tokens (MCP)' })).toHaveAttribute('href', '/einstellungen/api-tokens')
    expect(screen.getByRole('link', { name: 'Verfahrensdokumentation' })).toHaveAttribute(
      'href',
      '/einstellungen/verfahrensdoku',
    )
  })

  it('schaltet die Darstellung um und setzt data-theme', async () => {
    const { user } = await oeffnen()
    await user.click(screen.getByRole('button', { name: 'Dunkel' }))
    expect(document.documentElement.dataset.theme).toBe('dark')
    expect(screen.getByRole('button', { name: 'Dunkel' })).toHaveAttribute('aria-pressed', 'true')

    await user.click(screen.getByRole('button', { name: 'Hell' }))
    expect(document.documentElement.dataset.theme).toBe('light')
  })

  it('meldet ab und führt nach /anmelden', async () => {
    const { user, router } = await oeffnen()
    await user.click(screen.getByRole('button', { name: 'Abmelden' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/anmelden'))
    expect(db.angemeldet).toBe(false)
  })
})
