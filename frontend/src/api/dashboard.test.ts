import { describe, expect, it } from 'vitest'
import { szenario } from '@/mocks/szenarien'
import { useBackupStatus, useDashboard, useSystemStatus } from './dashboard'
import { erwarteDaten, renderMitQuery } from './testHilfe'

describe('Dashboard-Hooks', () => {
  it('useDashboard liefert die Zahlen des Designs', async () => {
    const { result } = renderMitQuery(() => useDashboard({ zeitraum: 'Monat', jahr: 2026 }))
    const dashboard = await erwarteDaten(() => result.current)
    expect(dashboard.kostenJahr.betrag).toBe(20845)
    expect(dashboard.zuPruefenListe).toHaveLength(5)
    expect(dashboard.topKorrespondenten[0].korrespondent.name).toBe('Autohaus Brenner GmbH')
    expect(dashboard.kostenProMonat).toHaveLength(12)
  })

  it('useDashboard ist im leeren Archiv leer', async () => {
    szenario.leer()
    const { result } = renderMitQuery(() => useDashboard())
    const dashboard = await erwarteDaten(() => result.current)
    expect(dashboard.zuPruefenListe).toEqual([])
    expect(dashboard.topKorrespondenten).toEqual([])
  })

  it('useSystemStatus und useBackupStatus', async () => {
    const { result } = renderMitQuery(() => ({ status: useSystemStatus(), backup: useBackupStatus() }))
    expect((await erwarteDaten(() => result.current.status)).length).toBeGreaterThan(0)
    expect((await erwarteDaten(() => result.current.backup)).anzahlDumps).toBeGreaterThan(0)
  })
})
