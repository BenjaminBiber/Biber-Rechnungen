/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import type { HttpHandler } from 'msw'
import { describe, expect, it } from 'vitest'
import { handlers, routen } from './handlers'
import { mswPfad } from './handlers/gemeinsam'

type Operation = { operationId: string; 'x-richtlinie'?: string }
type Vertrag = { paths: Record<string, Record<string, Operation>> }

const vertrag = JSON.parse(
  readFileSync(resolve(import.meta.dirname, '../../../docs/api/openapi.json'), 'utf8'),
) as Vertrag

const methoden = ['get', 'post', 'put', 'delete', 'patch']

const endpunkte = Object.entries(vertrag.paths).flatMap(([pfad, eintrag]) =>
  Object.entries(eintrag)
    .filter(([methode]) => methoden.includes(methode))
    .map(([methode, operation]) => ({ methode, pfad, operation })),
)

const schluessel = (methode: string, pfad: string) => `${methode.toUpperCase()} ${pfad}`

describe('Mock-Server deckt den API-Vertrag ab', () => {
  it('kennt Endpunkte aus docs/api/openapi.json', () => {
    expect(endpunkte.length).toBeGreaterThan(100)
  })

  it.each(endpunkte.map((e) => [schluessel(e.methode, e.pfad), e] as const))('%s hat einen MSW-Handler', (_, e) => {
    const vorhanden = handlers.some((h) => {
      const info = (h as HttpHandler).info
      return String(info.method).toLowerCase() === e.methode && info.path === mswPfad(e.pfad)
    })
    expect(vorhanden).toBe(true)
  })

  it('jeder Handler verlangt dieselbe Richtlinie wie der Vertrag (x-richtlinie)', () => {
    const abweichungen = endpunkte.flatMap((e) => {
      const r = routen.find((x) => x.methode === e.methode && x.pfad === e.pfad)
      const erwartet = e.operation['x-richtlinie'] ?? 'Anonym'
      return r && r.richtlinie !== erwartet
        ? [`${schluessel(e.methode, e.pfad)}: ${r.richtlinie} statt ${erwartet}`]
        : []
    })
    expect(abweichungen).toEqual([])
  })

  it('hat keine Handler außerhalb des Vertrags und keine doppelten', () => {
    const imVertrag = new Set(endpunkte.map((e) => schluessel(e.methode, e.pfad)))
    const imMock = routen.map((r) => schluessel(r.methode, r.pfad))
    expect(imMock.filter((s) => !imVertrag.has(s))).toEqual([])
    expect(imMock.filter((s, i) => imMock.indexOf(s) !== i)).toEqual([])
  })
})
