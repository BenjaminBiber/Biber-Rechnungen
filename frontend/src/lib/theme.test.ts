import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DARSTELLUNG_SCHLUESSEL, useDarstellung, wendeDarstellungAn } from './theme'

type Zuhoerer = (e: { matches: boolean }) => void

/** Simuliert `prefers-color-scheme` und erlaubt einen Wechsel während des Tests. */
function simuliereSystem(dunkel: boolean) {
  const zuhoerer = new Set<Zuhoerer>()
  const abfrage = {
    matches: dunkel,
    addEventListener: (_: string, z: Zuhoerer) => zuhoerer.add(z),
    removeEventListener: (_: string, z: Zuhoerer) => zuhoerer.delete(z),
  }
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => abfrage),
  )
  return {
    wechsleZu(neuDunkel: boolean) {
      abfrage.matches = neuDunkel
      zuhoerer.forEach((z) => z({ matches: neuDunkel }))
    },
    anzahlZuhoerer: () => zuhoerer.size,
  }
}

const theme = () => document.documentElement.dataset.theme

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('useDarstellung', () => {
  it('startet ohne gespeicherten Wert mit „system“ und folgt dem System', () => {
    simuliereSystem(true)
    const { result } = renderHook(() => useDarstellung())
    expect(result.current[0]).toBe('system')
    expect(theme()).toBe('dark')
  })

  it('liest den gespeicherten Wert aus dem localStorage', () => {
    simuliereSystem(false)
    window.localStorage.setItem(DARSTELLUNG_SCHLUESSEL, 'dunkel')
    const { result } = renderHook(() => useDarstellung())
    expect(result.current[0]).toBe('dunkel')
    expect(theme()).toBe('dark')
  })

  it('ignoriert ungültige gespeicherte Werte', () => {
    simuliereSystem(false)
    window.localStorage.setItem(DARSTELLUNG_SCHLUESSEL, 'lila')
    const { result } = renderHook(() => useDarstellung())
    expect(result.current[0]).toBe('system')
    expect(theme()).toBe('light')
  })

  it('speichert die Wahl und setzt data-theme am <html>', () => {
    simuliereSystem(false)
    const { result } = renderHook(() => useDarstellung())

    act(() => result.current[1]('dunkel'))
    expect(result.current[0]).toBe('dunkel')
    expect(window.localStorage.getItem(DARSTELLUNG_SCHLUESSEL)).toBe('dunkel')
    expect(theme()).toBe('dark')

    act(() => result.current[1]('hell'))
    expect(result.current[0]).toBe('hell')
    expect(window.localStorage.getItem(DARSTELLUNG_SCHLUESSEL)).toBe('hell')
    expect(theme()).toBe('light')
  })

  it('hält mehrere Nutzer des Hooks synchron', () => {
    simuliereSystem(false)
    const a = renderHook(() => useDarstellung())
    const b = renderHook(() => useDarstellung())
    act(() => a.result.current[1]('dunkel'))
    expect(b.result.current[0]).toBe('dunkel')
  })

  it('folgt bei „system“ einem Wechsel des Systems und hört bei fester Wahl auf', () => {
    const system = simuliereSystem(false)
    const { result } = renderHook(() => useDarstellung())
    expect(theme()).toBe('light')

    act(() => system.wechsleZu(true))
    expect(theme()).toBe('dark')

    act(() => result.current[1]('hell'))
    expect(system.anzahlZuhoerer()).toBe(0)
    act(() => system.wechsleZu(true))
    expect(theme()).toBe('light')
  })
})

describe('wendeDarstellungAn', () => {
  it('setzt ohne matchMedia bei „system“ das helle Schema', () => {
    vi.stubGlobal('matchMedia', undefined)
    wendeDarstellungAn('system')
    expect(theme()).toBe('light')
  })
})
