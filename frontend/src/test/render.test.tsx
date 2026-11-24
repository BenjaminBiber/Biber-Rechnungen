import { useQuery } from '@tanstack/react-query'
import { useLocation } from '@tanstack/react-router'
import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/mocks/server'
import { renderMitApp } from './render'

function Pfad() {
  const { pathname } = useLocation()
  return <p>Pfad: {pathname}</p>
}

function Gruss() {
  const { data } = useQuery({
    queryKey: ['gruss'],
    queryFn: async () => (await fetch(new URL('/api/gruss', window.location.href))).json() as Promise<{ text: string }>,
  })
  return <p>{data?.text ?? 'lädt'}</p>
}

describe('renderMitApp', () => {
  it('stellt einen Router mit der gewünschten Start-URL bereit', async () => {
    renderMitApp(<Pfad />, { route: '/belege/42' })
    expect(await screen.findByText('Pfad: /belege/42')).toBeInTheDocument()
  })

  it('startet ohne Angabe auf „/“', async () => {
    renderMitApp(<Pfad />)
    expect(await screen.findByText('Pfad: /')).toBeInTheDocument()
  })

  it('stellt einen QueryClient bereit, der gegen MSW-Handler abfragt', async () => {
    server.use(http.get('*/api/gruss', () => HttpResponse.json({ text: 'Hallo' })))
    renderMitApp(<Gruss />)
    expect(await screen.findByText('Hallo')).toBeInTheDocument()
  })
})
