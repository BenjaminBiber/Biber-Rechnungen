import { useNavigate } from '@tanstack/react-router'
import { useRef } from 'react'
import { belegDateiUrl } from '@/api/belege'
import type { BelegDetail } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { formatDatum } from '@/lib/format'
import { original, xmlDatei } from './gemeinsam'

/** Beleg-Aktionen „Mehr“ (⋯ neben „Bestätigen“). NurLesen sieht nur Herunterladen und Kopieren. */
export function MehrMenue({ beleg, darfBearbeiten }: { beleg: BelegDetail; darfBearbeiten: boolean }) {
  const navigate = useNavigate()
  const toast = useToast()
  // „Belegart ändern“ setzt den Fokus ins Formular – das Menü darf ihn beim Schließen nicht zurückholen.
  const fokusBehalten = useRef(false)
  const datei = original(beleg)
  const xml = xmlDatei(beleg)

  async function kopieren(text: string) {
    try {
      await navigator.clipboard.writeText(text)
      toast.erfolg('Kopiert')
    } catch {
      toast.fehler('Kopieren nicht möglich')
    }
  }

  function belegartAendern() {
    fokusBehalten.current = true
    void navigate({
      to: '/belege/$belegId',
      params: { belegId: beleg.id },
      search: (alt) => ({ ...alt, fokus: 'belegart' }),
    })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button type="button" className="btn" aria-label="Weitere Aktionen">
          <Icon name="more_horiz" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="w-[290px]"
        onCloseAutoFocus={(e) => {
          if (fokusBehalten.current) e.preventDefault()
          fokusBehalten.current = false
        }}
      >
        {datei && (
          <DropdownMenuItem asChild>
            <a href={belegDateiUrl(beleg.id, datei.id)} download className="text-text no-underline hover:text-text">
              <Icon name="download" />
              Original herunterladen
            </a>
          </DropdownMenuItem>
        )}
        {xml && (
          <DropdownMenuItem asChild>
            <a href={belegDateiUrl(beleg.id, xml.id)} download className="text-text no-underline hover:text-text">
              <Icon name="code" />
              XML herunterladen
              <DropdownMenuShortcut>E-Rechnung</DropdownMenuShortcut>
            </a>
          </DropdownMenuItem>
        )}
        {datei && (
          <DropdownMenuItem onSelect={() => void kopieren(datei.pfad)}>
            <Icon name="folder_copy" />
            Ablagepfad kopieren
          </DropdownMenuItem>
        )}
        <DropdownMenuItem onSelect={() => void kopieren(`${window.location.origin}/belege/${beleg.id}`)}>
          <Icon name="link" />
          Link kopieren
        </DropdownMenuItem>
        {darfBearbeiten && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => void navigate({ href: `/belege/${beleg.id}/dubletten?markieren=1` })}>
              <Icon name="content_copy" />
              Als Dublette markieren …
            </DropdownMenuItem>
            <DropdownMenuItem
              disabled={!beleg.korrespondent}
              onSelect={() =>
                beleg.korrespondent &&
                void navigate({ href: `/korrespondenten/${beleg.korrespondent.id}?logoAus=${beleg.id}` })
              }
            >
              <Icon name="crop" />
              Logo aus diesem Beleg übernehmen
            </DropdownMenuItem>
            <DropdownMenuItem onSelect={belegartAendern}>
              <Icon name="swap_horiz" />
              Belegart ändern
            </DropdownMenuItem>
          </>
        )}
        <DropdownMenuSeparator />
        <DropdownMenuItem disabled className="h-auto py-2">
          <Icon name="lock" />
          <span className="leading-tight">
            Löschen
            <br />
            <span className="text-xs">gesperrt bis {formatDatum(beleg.loeschsperreBis)}</span>
          </span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
