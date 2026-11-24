import { describe, expect, it } from 'vitest'
import { befehle, befehleFiltern } from './befehle'

describe('befehle', () => {
  it('Export gilt dem letzten Quartal', () => {
    const export_ = befehle({ rolle: 'Admin', heute: new Date(2026, 10, 24) }).find((b) => b.id === 'export')!
    expect(export_.text).toBe('Export Q3 2026 erstellen')
    expect(export_.ziel).toEqual({ art: 'route', to: '/export', search: { von: '2026-07-01', bis: '2026-09-30' } })

    const januar = befehle({ rolle: 'Admin', heute: new Date(2027, 0, 5) }).find((b) => b.id === 'export')!
    expect(januar.text).toBe('Export Q4 2026 erstellen')
  })

  it('NurLesen ohne „Beleg hochladen“', () => {
    const texte = (rolle: 'Admin' | 'NurLesen') => befehle({ rolle, heute: new Date() }).map((b) => b.text)
    expect(texte('Admin')).toContain('Beleg hochladen')
    expect(texte('NurLesen')).not.toContain('Beleg hochladen')
  })

  it('Kürzel wie im Design', () => {
    const kuerzel = Object.fromEntries(befehle({ rolle: 'Benutzer', heute: new Date() }).map((b) => [b.id, b.kuerzel]))
    expect(kuerzel).toMatchObject({
      hochladen: 'U',
      'zu-pruefen': 'G P',
      inbox: 'G I',
      dashboard: 'G D',
      einstellungen: 'G E',
    })
  })

  it('filtert nach der Eingabe', () => {
    const liste = befehle({ rolle: 'Admin', heute: new Date() })
    expect(befehleFiltern(liste, 'darstellung').map((b) => b.id)).toEqual(['hell', 'dunkel', 'system'])
    expect(befehleFiltern(liste, '  ')).toHaveLength(liste.length)
  })
})
