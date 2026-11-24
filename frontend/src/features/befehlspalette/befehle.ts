import type { Rolle } from '@/api/typen'
import type { Darstellung } from '@/lib/theme'
import { zeitraumVorgabe } from '@/lib/zeitraum'

/** Wohin ein Befehl führt: eine Route (mit Suchparametern) oder eine Aktion ohne Navigation. */
export type BefehlZiel =
  | { art: 'route'; to: string; search?: Record<string, string> }
  | { art: 'darstellung'; darstellung: Darstellung }
  | { art: 'abmelden' }

export type Befehl = {
  id: string
  text: string
  icon: string
  /** Tastenkürzel wie „U“ oder „G P“ (Großbuchstaben, Leerzeichen trennt die Folge). */
  kuerzel?: string
  ziel: BefehlZiel
}

/** „Q3 2026“ für den Quartalsbeginn „2026-07-01“. */
function quartal(von: string): string {
  const [jahr, monat] = von.split('-').map(Number)
  return `Q${Math.floor((monat - 1) / 3) + 1} ${jahr}`
}

/** Befehle der Palette; „Beleg hochladen“ fehlt für NurLesen, der Export gilt dem letzten Quartal vor `heute`. */
export function befehle(kontext: { rolle: Rolle; heute: Date }): Befehl[] {
  const letztes = zeitraumVorgabe('letztesQuartal', kontext.heute)
  const liste: Befehl[] = [
    {
      id: 'hochladen',
      text: 'Beleg hochladen',
      icon: 'upload_file',
      kuerzel: 'U',
      ziel: { art: 'route', to: '/hochladen' },
    },
    {
      id: 'zu-pruefen',
      text: 'Zu prüfen öffnen',
      icon: 'report',
      kuerzel: 'G P',
      ziel: { art: 'route', to: '/belege', search: { ansicht: 'ZuPruefen' } },
    },
    {
      id: 'inbox',
      text: 'Inbox öffnen',
      icon: 'inbox',
      kuerzel: 'G I',
      ziel: { art: 'route', to: '/belege', search: { ansicht: 'Inbox' } },
    },
    { id: 'dashboard', text: 'Dashboard', icon: 'dashboard', kuerzel: 'G D', ziel: { art: 'route', to: '/' } },
    {
      id: 'export',
      text: `Export ${quartal(letztes.von)} erstellen`,
      icon: 'download',
      ziel: { art: 'route', to: '/export', search: { von: letztes.von, bis: letztes.bis } },
    },
    {
      id: 'einstellungen',
      text: 'Einstellungen',
      icon: 'settings',
      kuerzel: 'G E',
      ziel: { art: 'route', to: '/einstellungen' },
    },
    {
      id: 'hell',
      text: 'Darstellung wechseln: Hell',
      icon: 'light_mode',
      ziel: { art: 'darstellung', darstellung: 'hell' },
    },
    {
      id: 'dunkel',
      text: 'Darstellung wechseln: Dunkel',
      icon: 'dark_mode',
      ziel: { art: 'darstellung', darstellung: 'dunkel' },
    },
    {
      id: 'system',
      text: 'Darstellung wechseln: System',
      icon: 'contrast',
      ziel: { art: 'darstellung', darstellung: 'system' },
    },
    { id: 'abmelden', text: 'Abmelden', icon: 'logout', ziel: { art: 'abmelden' } },
  ]
  return kontext.rolle === 'NurLesen' ? liste.filter((b) => b.id !== 'hochladen') : liste
}

/** Befehle, deren Text die Eingabe enthält (ohne Groß-/Kleinschreibung). */
export function befehleFiltern(liste: Befehl[], eingabe: string): Befehl[] {
  const q = eingabe.trim().toLocaleLowerCase('de')
  return q ? liste.filter((b) => b.text.toLocaleLowerCase('de').includes(q)) : liste
}
