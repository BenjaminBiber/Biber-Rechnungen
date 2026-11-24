export type NavEintrag = {
  id: string
  icon: string
  label: string
  to: '/' | '/belege' | '/hochladen' | '/korrespondenten' | '/artikel' | '/tags' | '/export' | '/einstellungen'
}

/** Alle Einträge der Seitenleiste (Sidebar.dc.html), nach Gruppen. */
export const navGruppen: Array<{ label: string; eintraege: NavEintrag[] }> = [
  {
    label: 'Übersicht',
    eintraege: [
      { id: 'dashboard', icon: 'space_dashboard', label: 'Dashboard', to: '/' },
      { id: 'belege', icon: 'receipt_long', label: 'Belege', to: '/belege' },
      { id: 'upload', icon: 'upload_file', label: 'Hochladen', to: '/hochladen' },
    ],
  },
  {
    label: 'Stammdaten',
    eintraege: [
      { id: 'korrespondenten', icon: 'storefront', label: 'Korrespondenten', to: '/korrespondenten' },
      { id: 'artikel', icon: 'inventory_2', label: 'Artikel', to: '/artikel' },
      { id: 'tags', icon: 'sell', label: 'Tags & Kategorien', to: '/tags' },
    ],
  },
  { label: 'Auswertung', eintraege: [{ id: 'export', icon: 'download', label: 'Export', to: '/export' }] },
  {
    label: 'System',
    eintraege: [{ id: 'einstellungen', icon: 'settings', label: 'Einstellungen', to: '/einstellungen' }],
  },
]

export const navEintraege: NavEintrag[] = navGruppen.flatMap((g) => g.eintraege)

/** Titel für die mobile Kopfzeile: Eintrag, unter dessen Pfad die Seite liegt. */
export function seitentitel(pfad: string): string {
  if (pfad.startsWith('/benachrichtigungen')) return 'Benachrichtigungen'
  const eintrag = navEintraege.find((e) => e.to !== '/' && (pfad === e.to || pfad.startsWith(e.to + '/')))
  return eintrag?.label ?? 'Dashboard'
}
