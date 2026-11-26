import { useSystemStatus } from '@/api/dashboard'
import type { SystemStatusEintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { SkelettListe } from '@/components/ui/skelett'
import { DashboardKarte } from './gemeinsam'

const icons: Record<string, string> = {
  integritaet: 'verified_user',
  backup: 'database',
  postfach: 'mail',
  eingangsordner: 'folder_open',
}

const farben: Record<SystemStatusEintrag['schweregrad'], string> = {
  Ok: 'var(--color-gruen-text)',
  Info: 'var(--color-akzent-text)',
  Warnung: 'var(--color-orange-text)',
  Fehler: 'var(--color-rot-text)',
}

/** „Ablage & Sicherung“: Statusmeldungen aus `/api/system/status`. */
export function SystemKarte() {
  const status = useSystemStatus()
  return (
    <DashboardKarte titel="Ablage & Sicherung">
      {status.isPending && <SkelettListe zeilen={3} kompakt label="Systemstatus wird geladen" />}
      {status.isError && (
        <div role="alert" className="flex flex-col items-start gap-2 text-sm">
          <span className="text-[var(--color-rot-text)]">Systemstatus konnte nicht geladen werden.</span>
          <button
            type="button"
            className="btn btn-klein"
            onClick={() => void status.refetch()}
            aria-busy={status.isFetching || undefined}
          >
            Erneut versuchen
          </button>
        </div>
      )}
      {status.data?.map((e) => (
        <div key={e.schluessel} className="flex items-start gap-2.5">
          <span style={{ color: farben[e.schweregrad] }}>
            <Icon name={icons[e.schluessel] ?? 'info'} label={e.schweregrad} />
          </span>
          <span className="text-sm">
            <strong>{e.titel}</strong>
            <br />
            <span className="gedaempft text-[13px]">{e.text}</span>
          </span>
        </div>
      ))}
    </DashboardKarte>
  )
}
