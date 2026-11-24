import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { renderMitApp } from '@/test/render'
import { Glocke } from './Glocke'

describe('Glocke', () => {
  it('zeigt den Zähler der ungelesenen Benachrichtigungen', async () => {
    renderMitApp(<Glocke />)
    expect(await screen.findByRole('button', { name: 'Benachrichtigungen, 3 ungelesen' })).toBeInTheDocument()
    expect(screen.getByTestId('glocke-zaehler')).toHaveTextContent('3')
  })

  it('öffnet das Popover; „Alle gelesen“ setzt den Zähler auf 0', async () => {
    const user = userEvent.setup()
    renderMitApp(<Glocke />)
    await user.click(await screen.findByRole('button', { name: 'Benachrichtigungen, 3 ungelesen' }))
    const popover = await screen.findByRole('dialog', { name: 'Benachrichtigungen' })
    expect(await screen.findByText('Document Intelligence bei 82 %')).toBeInTheDocument()
    expect(popover).toHaveTextContent('Alle Benachrichtigungen')

    await user.click(screen.getByRole('button', { name: 'Alle gelesen' }))
    expect(await screen.findByRole('button', { name: 'Benachrichtigungen, 0 ungelesen' })).toBeInTheDocument()
    expect(screen.queryByTestId('glocke-zaehler')).not.toBeInTheDocument()
  })

  it('Klick auf einen Eintrag markiert ihn als gelesen und folgt dem Link', async () => {
    const user = userEvent.setup()
    const { router } = renderMitApp(<Glocke />)
    await user.click(await screen.findByRole('button', { name: 'Benachrichtigungen, 3 ungelesen' }))
    await user.click(await screen.findByRole('button', { name: /Import fehlgeschlagen/ }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/hochladen'))
    expect(await screen.findByRole('button', { name: 'Benachrichtigungen, 2 ungelesen' })).toBeInTheDocument()
  })
})
