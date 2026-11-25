import { Link } from '@tanstack/react-router'
import { Icon } from '@/components/Icon'
import { useBefehlspalette } from './befehlspalette-store'
import { Benutzermenue } from './Benutzermenue'
import { Glocke } from './Glocke'
import { useRolle } from './useRolle'

/** Kopfleiste ab 1024 px (Topbar.dc.html): Suche (öffnet die Befehlspalette), Hochladen, Glocke, Benutzermenü. */
export function Kopfleiste() {
  const { oeffnen } = useBefehlspalette()
  const { darfBearbeiten } = useRolle()

  return (
    <header className="relative z-30 flex min-h-11 flex-wrap items-center gap-2.5 border-b border-rand bg-karte px-6 py-[5px] text-text xl:px-8">
      <button
        type="button"
        onClick={oeffnen}
        aria-label="Volltextsuche"
        className="flex h-8 max-w-[520px] flex-[1_1_280px] cursor-text items-center gap-2 rounded-lg border border-eingabe-rand bg-[#f7f8fa] px-3 text-left dark:bg-flaeche"
      >
        <Icon name="search" className="text-[18px] text-gedaempft" />
        <span className="min-w-0 flex-1 truncate text-sm text-gedaempft">
          Belege, Korrespondenten, Artikel durchsuchen …
        </span>
        <kbd className="rounded border border-eingabe-rand bg-karte px-[5px] py-px font-mono text-[11px] font-medium text-gedaempft-stark">
          Strg K
        </kbd>
      </button>
      <div className="flex-[1_1_0]" />
      {darfBearbeiten && (
        <Link to="/hochladen" className="btn btn-primaer h-8 px-3 text-[13px]">
          <Icon name="upload" className="text-[18px]" />
          Beleg hochladen
        </Link>
      )}
      <Glocke />
      <Benutzermenue />
    </header>
  )
}
