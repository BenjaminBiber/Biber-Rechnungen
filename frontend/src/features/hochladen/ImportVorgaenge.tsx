import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useImportVorgaenge } from '@/api/import'
import type { ImportQuelle, ImportStatus, ImportVorgang } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatDatumZeit } from '@/lib/format'

const quellen: { wert?: ImportQuelle; text: string; icon: string }[] = [
  { text: 'Alle Quellen', icon: '' },
  { wert: 'Upload', text: 'Upload', icon: 'upload' },
  { wert: 'Ordner', text: 'Ordner', icon: 'folder_open' },
  { wert: 'Mail', text: 'E-Mail', icon: 'mail' },
  { wert: 'Mcp', text: 'MCP', icon: 'smart_toy' },
]

const zeitraeume = [
  { tage: 1, text: 'Heute' },
  { tage: 7, text: 'Letzte 7 Tage' },
  { tage: 30, text: 'Letzte 30 Tage' },
]

const statusChip: Record<ImportStatus, { ton: ChipTon; text: string }> = {
  Angenommen: { ton: 'blau', text: 'läuft' },
  InVerarbeitung: { ton: 'blau', text: 'läuft' },
  Fertig: { ton: 'gruen', text: 'fertig' },
  Wartet: { ton: 'orange', text: 'wartet' },
  Pausiert: { ton: 'orange', text: 'pausiert' },
  OhneBeleg: { ton: 'grau', text: 'ohne Beleg' },
  Dublette: { ton: 'orange', text: 'Dublette' },
  Fehler: { ton: 'rot', text: 'Fehler' },
  Abgelehnt: { ton: 'rot', text: 'abgelehnt' },
}

const uhrzeit = (iso: string) => new Date(iso).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })

/** Karte „Import-Vorgänge“ aller Quellen mit Filtern und Blättern; pollt, solange etwas läuft. */
export function ImportVorgaenge() {
  const [quelle, setQuelle] = useState<ImportQuelle | undefined>()
  const [tage, setTage] = useState(7)
  const [seite, setSeite] = useState(1)
  const { data, isPending, isError } = useImportVorgaenge({ ...(quelle ? { quelle } : {}), tage, seite })
  const seiten = data ? Math.max(1, Math.ceil(data.gesamt / data.seitengroesse)) : 1

  return (
    <section className="karte overflow-x-auto" aria-labelledby="import-vorgaenge">
      <div className="flex flex-wrap items-center justify-between gap-2.5 px-5 py-4">
        <h2 id="import-vorgaenge" className="m-0 text-base font-semibold">
          Import-Vorgänge
        </h2>
        <div className="flex flex-wrap gap-3">
          <div className="segment" role="group" aria-label="Quelle">
            {quellen.map((q) => (
              <button
                key={q.text}
                type="button"
                aria-pressed={quelle === q.wert}
                onClick={() => {
                  setQuelle(q.wert)
                  setSeite(1)
                }}
              >
                {q.text}
              </button>
            ))}
          </div>
          <div className="segment" role="group" aria-label="Zeitraum">
            {zeitraeume.map((z) => (
              <button
                key={z.tage}
                type="button"
                aria-pressed={tage === z.tage}
                onClick={() => {
                  setTage(z.tage)
                  setSeite(1)
                }}
              >
                {z.text}
              </button>
            ))}
          </div>
        </div>
      </div>
      <table className="tabelle min-w-[860px]">
        <thead>
          <tr>
            <th scope="col">Zeitpunkt</th>
            <th scope="col">Quelle</th>
            <th scope="col">Herkunft</th>
            <th scope="col">Status</th>
            <th scope="col">Versuche</th>
            <th scope="col">Ergebnis</th>
          </tr>
        </thead>
        <tbody>
          {data?.eintraege.map((v) => (
            <Zeile key={v.id} vorgang={v} />
          ))}
          {data && !data.eintraege.length && (
            <tr>
              <td colSpan={6} className="gedaempft">
                Keine Import-Vorgänge im gewählten Zeitraum.
              </td>
            </tr>
          )}
          {isPending && (
            <tr>
              <td colSpan={6} className="gedaempft">
                Lädt …
              </td>
            </tr>
          )}
          {isError && (
            <tr>
              <td colSpan={6} className="text-rot-text">
                Import-Vorgänge konnten nicht geladen werden.
              </td>
            </tr>
          )}
        </tbody>
      </table>
      {seiten > 1 && (
        <div className="flex items-center justify-end gap-2 px-5 py-3 text-sm">
          <button type="button" className="btn btn-klein" disabled={seite <= 1} onClick={() => setSeite(seite - 1)}>
            Zurück
          </button>
          <span className="gedaempft">
            Seite {seite} von {seiten}
          </span>
          <button
            type="button"
            className="btn btn-klein"
            disabled={seite >= seiten}
            onClick={() => setSeite(seite + 1)}
          >
            Weiter
          </button>
        </div>
      )}
    </section>
  )
}

function Zeile({ vorgang: v }: { vorgang: ImportVorgang }) {
  const q = quellen.find((x) => x.wert === v.quelle) ?? quellen[1]
  const s = statusChip[v.status]
  const istMail = v.quelle === 'Mail'
  const herkunft1 = istMail ? `„${v.herkunftDetail ?? v.dateiname}“` : v.dateiname || v.herkunft
  const herkunft2 = istMail ? `von ${v.herkunft}` : [v.herkunft, v.herkunftDetail].filter(Boolean).join(' · ')
  return (
    <tr>
      <td className="whitespace-nowrap">{formatDatumZeit(v.zeitpunkt)}</td>
      <td>
        <span className="inline-flex items-center gap-1.5">
          <Icon name={q.icon} className="text-[18px] text-gedaempft-stark" />
          {q.text}
        </span>
      </td>
      <td className="max-w-[360px]">
        <div className="text-[13px]">{herkunft1}</div>
        <div className="gedaempft text-xs">{herkunft2}</div>
      </td>
      <td>
        <Chip ton={s.ton}>{s.text}</Chip>
      </td>
      <td>{v.versuche > 1 ? `${v.versuche} / ${v.maxVersuche}` : v.versuche}</td>
      <td className="text-[13px]">
        <Ergebnis vorgang={v} />
      </td>
    </tr>
  )
}

function Ergebnis({ vorgang: v }: { vorgang: ImportVorgang }) {
  const text = v.ergebnis ?? v.fehler ?? '—'
  if (v.status === 'Fertig' && v.belegId)
    return (
      <Link to="/belege/$belegId" params={{ belegId: v.belegId }} className="text-akzent-text">
        {text}
      </Link>
    )
  if (v.status === 'Wartet' && v.naechsterVersuchAm)
    return (
      <>
        {text} · nächster Versuch {uhrzeit(v.naechsterVersuchAm)}
      </>
    )
  return <>{text}</>
}
