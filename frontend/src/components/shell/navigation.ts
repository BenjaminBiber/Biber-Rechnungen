import type { KubaiIconName } from '@/components/kubai/KubaiIcon'

export type NavEintrag = {
  id: string
  /** Material-Symbol (mobile Navigation, Befehlspalette). */
  icon: string
  /** Animiertes Icon der Seitenleiste. */
  kubai: KubaiIconName
  label: string
  to: '/' | '/belege' | '/hochladen' | '/korrespondenten' | '/artikel' | '/tags' | '/export' | '/einstellungen'
}

/** Alle Einträge der Seitenleiste (Sidebar.dc.html), nach Gruppen. */
export const navGruppen: Array<{ label: string; eintraege: NavEintrag[] }> = [
  {
    label: 'Übersicht',
    eintraege: [
      { id: 'dashboard', kubai: 'Dashboard', icon: 'space_dashboard', label: 'Dashboard', to: '/' },
      { id: 'belege', kubai: 'Belege', icon: 'receipt_long', label: 'Belege', to: '/belege' },
      { id: 'upload', kubai: 'Hochladen', icon: 'upload_file', label: 'Hochladen', to: '/hochladen' },
    ],
  },
  {
    label: 'Stammdaten',
    eintraege: [
      {
        id: 'korrespondenten',
        kubai: 'Korrespondenten',
        icon: 'storefront',
        label: 'Korrespondenten',
        to: '/korrespondenten',
      },
      { id: 'artikel', kubai: 'Artikel', icon: 'inventory_2', label: 'Artikel', to: '/artikel' },
      { id: 'tags', kubai: 'Tags', icon: 'sell', label: 'Tags & Kategorien', to: '/tags' },
    ],
  },
  {
    label: 'Auswertung',
    eintraege: [{ id: 'export', kubai: 'Export', icon: 'download', label: 'Export', to: '/export' }],
  },
  {
    label: 'System',
    eintraege: [
      { id: 'einstellungen', kubai: 'Einstellungen', icon: 'settings', label: 'Einstellungen', to: '/einstellungen' },
    ],
  },
]

export const navEintraege: NavEintrag[] = navGruppen.flatMap((g) => g.eintraege)

/** Titel für die mobile Kopfzeile: Eintrag, unter dessen Pfad die Seite liegt. */
export function seitentitel(pfad: string): string {
  if (pfad.startsWith('/benachrichtigungen')) return 'Benachrichtigungen'
  const eintrag = navEintraege.find((e) => e.to !== '/' && (pfad === e.to || pfad.startsWith(e.to + '/')))
  return eintrag?.label ?? 'Dashboard'
}
