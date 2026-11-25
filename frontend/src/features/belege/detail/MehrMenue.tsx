import { useNavigate } from '@tanstack/react-router'
import { useRef } from 'react'
import { belegDateiUrl, useErneutAuslesen } from '@/api/belege'
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

/**
 * Beleg-Aktionen neben „Bestätigen“ als geteilter Knopf: Hauptaktion „Original herunterladen“,
 * der Pfeil daneben öffnet das Menü mit „Erneut auslesen“ und den übrigen Aktionen.
 * Ohne Original bleibt nur der Menüknopf (⋯). NurLesen sieht nur Herunterladen und Kopieren.
 *
 * `kompakt` (mobile Kopfzeile): nur ⋯, „Original herunterladen“ steht dann im Menü; „Erneut auslesen“ hat dort
 * einen eigenen Knopf.
 */
export function MehrMenue({
  beleg,
  darfBearbeiten,
  kompakt = false,
}: {
  beleg: BelegDetail
  darfBearbeiten: boolean
  kompakt?: boolean
}) {
  const navigate = useNavigate()
  const toast = useToast()
  const erneutAuslesen = useErneutAuslesen()
  // „Belegart ändern“ setzt den Fokus ins Formular – das Menü darf ihn beim Schließen nicht zurückholen.
  const fokusBehalten = useRef(false)
  const datei = original(beleg)
  const xml = xmlDatei(beleg)
  const geteilt = !!datei && !kompakt

  async function auslesen() {
    try {
      await erneutAuslesen.mutateAsync(beleg.id)
      toast.info('Beleg wird erneut ausgelesen')
    } catch {
      toast.fehler('Erneut auslesen fehlgeschlagen')
    }
  }

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
    <div className="inline-flex" role="group" aria-label="Beleg-Aktionen">
      {geteilt && (
        <a
          href={belegDateiUrl(beleg.id, datei.id)}
          download
          className="btn rounded-r-none max-2xl:px-2.5"
          aria-label="Original herunterladen"
          title="Original herunterladen"
        >
          <Icon name="download" />
          {/* Laptop-Breiten: nur das Icon */}
          <span className="max-2xl:hidden">Herunterladen</span>
        </a>
      )}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            type="button"
            className={geteilt ? 'btn -ml-px rounded-l-none px-2' : 'btn'}
            aria-label="Weitere Aktionen"
          >
            <Icon name={geteilt ? 'expand_more' : 'more_horiz'} />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          align="end"
          className="w-[290px]"
          onCloseAutoFocus={(e) => {
            if (fokusBehalten.current) e.preventDefault()
            fokusBehalten.current = false
          }}
        >
          {darfBearbeiten && !kompakt && (
            <>
              <DropdownMenuItem disabled={erneutAuslesen.isPending} onSelect={() => void auslesen()}>
                <Icon name="refresh" />
                Erneut auslesen
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}
          {datei && !geteilt && (
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
    </div>
  )
}
