import { useBelegAuswahl } from './auswahl'

/** Leiste bei vorhandener Auswahl (ersetzt die Filterleiste). Die Aktionen folgen in einem eigenen Ticket. */
export function Massenaktionen() {
  const auswahl = useBelegAuswahl()
  const anzahl = auswahl.ids.length
  return (
    <div
      role="toolbar"
      aria-label="Massenaktionen"
      className="flex min-h-[60px] flex-wrap items-center gap-2 rounded-[10px] bg-akzent py-3 pr-3 pl-4 text-auf-akzent"
    >
      <span className="mr-2 text-sm font-semibold" aria-live="polite">
        {anzahl === 1 ? '1 Beleg ausgewählt' : `${anzahl} Belege ausgewählt`}
      </span>
      <span className="flex-1" />
      <button
        type="button"
        onClick={auswahl.leeren}
        className="cursor-pointer border-0 bg-transparent text-[13px] font-medium text-[#D7D6EE] hover:text-auf-akzent"
      >
        Auswahl aufheben
      </button>
    </div>
  )
}
