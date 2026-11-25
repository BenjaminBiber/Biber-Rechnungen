import { Fragment, useState } from 'react'
import { useBackupStatus } from '@/api/dashboard'
import { useIntegritaetsLaeufe, useIntegritaetStarten } from '@/api/integritaet'
import type { BackupStatus, IntegritaetsLauf } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { formatDatumZeit, formatZahl } from '@/lib/format'

/** Backups älter als das gelten nicht mehr als aktuell. */
const BACKUP_MAX_ALTER_MS = 36 * 60 * 60 * 1000

/** „heute 03:00“, „gestern 03:00“ oder Datum mit Uhrzeit. */
export function zeitpunkt(iso: string, jetzt = new Date()): string {
  const d = new Date(iso)
  const uhr = d.toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' })
  const tag = (x: Date) => x.toDateString()
  if (tag(d) === tag(jetzt)) return `heute ${uhr}`
  const gestern = new Date(jetzt)
  gestern.setDate(jetzt.getDate() - 1)
  if (tag(d) === tag(gestern)) return `gestern ${uhr}`
  return formatDatumZeit(iso)
}

/** „heute 03:00 · 2.184 Dateien · 0 Abweichungen · Dauer 4 Min.“ */
export function laufText(lauf: IntegritaetsLauf, jetzt = new Date()): string {
  const teile = [
    zeitpunkt(lauf.gestartetAm, jetzt),
    `${formatZahl(lauf.anzahlDateien)} Dateien`,
    `${formatZahl(lauf.anzahlAbweichungen)} Abweichungen`,
  ]
  if (lauf.dauerSekunden != null) teile.push(`Dauer ${Math.max(1, Math.round(lauf.dauerSekunden / 60))} Min.`)
  return teile.join(' · ')
}

/** „heute 02:00 · pg_dump 48 MB · 30 Tage rotierend“ oder `undefined`, wenn kein aktuelles Backup da ist. */
export function backupText(b: BackupStatus, jetzt = new Date()): string | undefined {
  if (!b.letztesBackup || jetzt.getTime() - new Date(b.letztesBackup).getTime() > BACKUP_MAX_ALTER_MS) return undefined
  const teile = [zeitpunkt(b.letztesBackup, jetzt)]
  if (b.groesse != null) teile.push(`pg_dump ${formatZahl(Math.round(b.groesse / (1024 * 1024)))} MB`)
  teile.push(`${b.anzahlDumps} Tage rotierend`)
  return teile.join(' · ')
}

const kasten = {
  gruen: 'bg-gruen-flaeche text-gruen-text',
  rot: 'bg-rot-flaeche text-rot-text',
  orange: 'bg-orange-flaeche text-orange-text',
  grau: 'bg-flaeche text-gedaempft-stark',
}

function Kasten({ titel, ton, children }: { titel: string; ton: keyof typeof kasten; children: string }) {
  return (
    <div className={`rounded-[10px] p-4 text-sm ${kasten[ton]}`} data-ton={ton}>
      <strong>{titel}</strong>
      <br />
      {children}
    </div>
  )
}

/** Bereich „Integrität & Sicherung“ (Einstellungen.dc.html → `s.integritaet`). */
export function IntegritaetBereich() {
  const { istAdmin } = useRolle()
  const toast = useToast()
  const { data: laeufe, isError, error } = useIntegritaetsLaeufe()
  const { data: backup } = useBackupStatus()
  const starten = useIntegritaetStarten()
  const [offen, setOffen] = useState<string>()
  const letzter = laeufe?.[0]
  const backupZeile = backup && backupText(backup)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <h2 className="m-0 text-lg font-semibold text-text">Integrität &amp; Sicherung</h2>
        {istAdmin && (
          <Button
            type="button"
            laedt={starten.isPending}
            onClick={() =>
              starten.mutateAsync().then(
                () => toast.erfolg('Integritätsprüfung gestartet'),
                (f: Error) => toast.fehler(f.message),
              )
            }
          >
            <Icon name="play_arrow" />
            Prüfung jetzt starten
          </Button>
        )}
      </div>
      <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
        {letzter ? (
          <Kasten titel="Letzte Integritätsprüfung" ton={letzter.anzahlAbweichungen > 0 ? 'rot' : 'gruen'}>
            {laufText(letzter)}
          </Kasten>
        ) : (
          <Kasten titel="Letzte Integritätsprüfung" ton="grau">
            {laeufe ? 'noch keine Prüfung gelaufen' : 'Lädt …'}
          </Kasten>
        )}
        {backup &&
          (backupZeile ? (
            <Kasten titel="Letztes DB-Backup" ton="gruen">
              {backupZeile}
            </Kasten>
          ) : (
            <Kasten titel="Letztes DB-Backup" ton="orange">
              Kein aktuelles Datenbank-Backup
            </Kasten>
          ))}
      </div>
      {laeufe ? (
        <table className="tabelle">
          <thead>
            <tr>
              <th scope="col">Lauf</th>
              <th scope="col" className="num">
                Dateien
              </th>
              <th scope="col" className="num">
                Abweichungen
              </th>
              <th scope="col">Ergebnis</th>
            </tr>
          </thead>
          <tbody>
            {laeufe.map((l) => {
              const abweichung = l.anzahlAbweichungen > 0
              const aufgeklappt = offen === l.id
              return (
                <Fragment key={l.id}>
                  <tr>
                    <td>
                      {abweichung ? (
                        <button
                          type="button"
                          className="inline-flex items-center gap-1 border-0 bg-transparent p-0 text-inherit"
                          aria-expanded={aufgeklappt}
                          onClick={() => setOffen(aufgeklappt ? undefined : l.id)}
                        >
                          <Icon name={aufgeklappt ? 'expand_less' : 'expand_more'} />
                          {formatDatumZeit(l.gestartetAm)}
                        </button>
                      ) : (
                        formatDatumZeit(l.gestartetAm)
                      )}
                    </td>
                    <td className="num">{formatZahl(l.anzahlDateien)}</td>
                    <td className={`num${abweichung ? ' text-rot-text' : ''}`}>{formatZahl(l.anzahlAbweichungen)}</td>
                    <td>{abweichung ? <Chip ton="rot">Abweichung</Chip> : <Chip ton="gruen">ok</Chip>}</td>
                  </tr>
                  {aufgeklappt && (
                    <tr>
                      <td colSpan={4}>
                        <ul aria-label="Abweichende Dateien" className="mono m-0 list-none p-0 text-xs">
                          {l.abweichungen.map((p) => (
                            <li key={p}>{p}</li>
                          ))}
                        </ul>
                      </td>
                    </tr>
                  )}
                </Fragment>
              )
            })}
          </tbody>
        </table>
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : null}
      <div className="flex gap-2.5 rounded-[10px] bg-flaeche px-3.5 py-3 text-[13px] text-gedaempft-stark">
        <Icon name="info" />
        <span>Ablage- und Backup-Ordner werden vom vorhandenen Host-Backup gesichert.</span>
      </div>
    </div>
  )
}
