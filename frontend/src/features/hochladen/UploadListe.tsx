import { Link } from '@tanstack/react-router'
import { useEffect } from 'react'
import { useBeleg } from '@/api/belege'
import { useImportVorgang } from '@/api/import'
import type { ImportSchritt } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatEuro } from '@/lib/format'
import { formatGroesse } from './pruefung'
import { warteschlange, type UploadEintrag, type UploadStatus } from './uploadWarteschlange'

const darstellung: Record<UploadStatus, { icon: string; ton: ChipTon; text: string }> = {
  laedt: { icon: 'upload', ton: 'blau', text: 'Hochladen' },
  verarbeitung: { icon: 'settings', ton: 'blau', text: 'In Verarbeitung' },
  fertig: { icon: 'task_alt', ton: 'gruen', text: 'Fertig' },
  dublette: { icon: 'content_copy', ton: 'orange', text: 'Dublette' },
  abgelehnt: { icon: 'error', ton: 'rot', text: 'Abgelehnt' },
  fehler: { icon: 'error', ton: 'rot', text: 'Fehler' },
}

const flaeche: Record<ChipTon, string> = {
  blau: 'bg-blau-flaeche text-blau-text',
  gruen: 'bg-gruen-flaeche text-gruen-text',
  orange: 'bg-orange-flaeche text-orange-text',
  rot: 'bg-rot-flaeche text-rot-text',
  grau: 'bg-grau-flaeche text-grau-text',
  petrol: 'bg-petrol-flaeche text-petrol-text',
  lila: 'bg-lila-flaeche text-lila-text',
}

/** Liste der in dieser Sitzung hochgeladenen Dateien. */
export function UploadListe({
  eintraege,
  onEntfernen,
  onErneut,
}: {
  eintraege: UploadEintrag[]
  onEntfernen: (id: string) => void
  onErneut: (id: string) => void
}) {
  if (!eintraege.length) return null
  return (
    <section className="karte" aria-label="Hochgeladene Dateien">
      <ul className="m-0 list-none p-0">
        {eintraege.map((e) => (
          <Zeile key={e.id} eintrag={e} onEntfernen={onEntfernen} onErneut={onErneut} />
        ))}
      </ul>
    </section>
  )
}

function Zeile({
  eintrag,
  onEntfernen,
  onErneut,
}: {
  eintrag: UploadEintrag
  onEntfernen: (id: string) => void
  onErneut: (id: string) => void
}) {
  const d = darstellung[eintrag.status]
  const abgeschlossen = eintrag.status !== 'laedt' && eintrag.status !== 'verarbeitung'
  const chip = eintrag.status === 'laedt' ? `Hochladen ${eintrag.prozent} %` : d.text
  return (
    <li className="flex items-start gap-3 border-b border-linie px-5 py-4 last:border-b-0" data-testid="upload-eintrag">
      {eintrag.status === 'verarbeitung' && eintrag.vorgang && <Verfolgung eintrag={eintrag} />}
      <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[10px] ${flaeche[d.ton]}`}>
        <Icon name={d.icon} />
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-1.5">
        <div className="flex flex-wrap items-center gap-2">
          <span className="truncate text-sm font-medium">{eintrag.name}</span>
          <span className="gedaempft text-xs">{formatGroesse(eintrag.groesse)}</span>
          <Chip ton={d.ton}>{chip}</Chip>
        </div>
        <Inhalt eintrag={eintrag} onErneut={onErneut} />
      </div>
      {abgeschlossen && (
        <button
          type="button"
          className="btn btn-klein shrink-0"
          aria-label={`${eintrag.name} entfernen`}
          onClick={() => onEntfernen(eintrag.id)}
        >
          <Icon name="close" />
        </button>
      )}
    </li>
  )
}

/** Verfolgt den Import-Vorgang (Polling) und meldet jeden Stand an die Warteschlange. */
function Verfolgung({ eintrag }: { eintrag: UploadEintrag }) {
  const { data } = useImportVorgang(eintrag.vorgang!.id)
  useEffect(() => {
    if (data) warteschlange.vorgangAktualisiert(eintrag.id, data)
  }, [data, eintrag.id])
  return null
}

function Inhalt({ eintrag, onErneut }: { eintrag: UploadEintrag; onErneut: (id: string) => void }) {
  const vorgang = eintrag.vorgang
  switch (eintrag.status) {
    case 'laedt':
      return (
        <>
          <div
            className="h-1.5 overflow-hidden rounded-full bg-linie"
            role="progressbar"
            aria-label={`Fortschritt ${eintrag.name}`}
            aria-valuenow={eintrag.prozent}
            aria-valuemin={0}
            aria-valuemax={100}
          >
            <div className="h-full bg-akzent" style={{ width: `${eintrag.prozent}%` }} />
          </div>
          <span className="gedaempft text-xs">Wird in den Staging-Bereich übertragen …</span>
        </>
      )
    case 'verarbeitung':
      return (
        <>
          <Schritte schritte={vorgang?.schritte ?? []} />
          {vorgang?.dateiname.toLowerCase().endsWith('.xml') && (
            <span className="gedaempft text-xs">
              E-Rechnung erkannt: XML ist maßgeblich, kein Document Intelligence nötig.
            </span>
          )}
        </>
      )
    case 'fertig':
      return vorgang?.belegId ? (
        <BelegNotiz belegId={vorgang.belegId} />
      ) : (
        <span className="gedaempft text-xs">{vorgang?.ergebnis ?? 'Fertig.'}</span>
      )
    case 'dublette':
      return (
        <span className="flex flex-wrap items-center gap-2 text-xs text-orange-text">
          {vorgang?.ergebnis ?? 'Dublette eines vorhandenen Belegs.'}
          {vorgang?.duplikatVonBelegId && (
            <Link to="/belege/$belegId" params={{ belegId: vorgang.duplikatVonBelegId }} className="font-medium">
              Original ansehen
            </Link>
          )}
        </span>
      )
    default:
      return (
        <span className="flex flex-wrap items-center gap-2 text-xs text-rot-text">
          {eintrag.meldung}
          {eintrag.wiederholbar && (
            <button type="button" className="btn btn-klein" onClick={() => onErneut(eintrag.id)}>
              <Icon name="refresh" />
              Erneut
            </button>
          )}
        </span>
      )
  }
}

function BelegNotiz({ belegId }: { belegId: string }) {
  const { data } = useBeleg(belegId)
  const teile = data
    ? [data.korrespondent?.name, data.rechnungsnummer, data.brutto == null ? null : formatEuro(data.brutto)].filter(
        Boolean,
      )
    : []
  return (
    <span className="flex flex-wrap items-center gap-2 text-xs">
      {teile.length > 0 && <span className="gedaempft">{teile.join(' · ')}</span>}
      <Link to="/belege/$belegId" params={{ belegId }} className="font-medium text-akzent-text">
        Beleg öffnen
      </Link>
    </span>
  )
}

const schrittTon: Record<string, { ton: ChipTon; icon: string; text: string }> = {
  ok: { ton: 'gruen', icon: 'check', text: 'erledigt' },
  laeuft: { ton: 'blau', icon: 'progress_activity', text: 'läuft' },
  fehler: { ton: 'rot', icon: 'error', text: 'Fehler' },
}

function Schritte({ schritte }: { schritte: ImportSchritt[] }) {
  return (
    <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0" aria-label="Verarbeitungsschritte">
      {schritte.map((s) => {
        const t = schrittTon[s.status] ?? { ton: 'grau' as const, icon: 'radio_button_unchecked', text: 'offen' }
        return (
          <li key={s.name} data-status={s.status}>
            <Chip ton={t.ton} icon={t.icon}>
              {s.name}
              <span className="sr-only"> ({t.text})</span>
            </Chip>
          </li>
        )
      })}
    </ul>
  )
}
