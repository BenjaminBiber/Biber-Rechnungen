import { Link, useNavigate, useRouterState } from '@tanstack/react-router'
import { useBenutzer } from '@/api/benutzer'
import { useDiVerbrauch, useEinstellungen } from '@/api/einstellungen'
import type { UstStatus } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'

type BereichPfad =
  | 'allgemein'
  | 'umsatzsteuer'
  | 'pruefungen'
  | 'aufbewahrung'
  | 'geloeschte-belege'
  | 'document-intelligence'
  | 'mail'
  | 'eingangsordner'
  | 'benutzer'
  | 'api-tokens'
  | 'integritaet'
  | 'konto'
  | 'verfahrensdoku'

type Bereich = { pfad: BereichPfad; icon: string; label: string }

/** Alle dreizehn Bereiche in drei Gruppen (Einstellungen.dc.html, Navigation links). */
export const bereichsGruppen: { label: string; bereiche: Bereich[] }[] = [
  {
    label: 'Fachlich',
    bereiche: [
      { pfad: 'allgemein', icon: 'tune', label: 'Allgemein' },
      { pfad: 'umsatzsteuer', icon: 'percent', label: 'Umsatzsteuer' },
      { pfad: 'pruefungen', icon: 'fact_check', label: 'Prüfungen' },
      { pfad: 'aufbewahrung', icon: 'lock_clock', label: 'Aufbewahrung' },
      { pfad: 'geloeschte-belege', icon: 'delete', label: 'Gelöschte Belege' },
    ],
  },
  {
    label: 'Eingang',
    bereiche: [
      { pfad: 'document-intelligence', icon: 'document_scanner', label: 'Document Intelligence' },
      { pfad: 'mail', icon: 'mail', label: 'E-Mail-Import' },
      { pfad: 'eingangsordner', icon: 'folder_open', label: 'Eingangsordner' },
    ],
  },
  {
    label: 'Zugriff & Betrieb',
    bereiche: [
      { pfad: 'benutzer', icon: 'group', label: 'Benutzer & Freigaben' },
      { pfad: 'api-tokens', icon: 'key', label: 'API-Tokens (MCP)' },
      { pfad: 'integritaet', icon: 'verified_user', label: 'Integrität & Sicherung' },
      { pfad: 'konto', icon: 'person', label: 'Mein Konto' },
      { pfad: 'verfahrensdoku', icon: 'description', label: 'Verfahrensdoku' },
    ],
  },
]

export const ustStatusText: Record<UstStatus, string> = {
  Unbekannt: 'unbekannt',
  Regelbesteuerung: 'Regelbesteuerung',
  Kleinunternehmer: 'Kleinunternehmer',
}

/** Anzahl wartender Konten – nur für Admins abgefragt. */
function WartendeKonten() {
  const { data } = useBenutzer()
  const anzahl = data?.filter((b) => !b.freigegeben).length ?? 0
  return anzahl > 0 ? (
    <span aria-label={`${anzahl} wartend`}>
      <Chip ton="blau">{anzahl}</Chip>
    </span>
  ) : null
}

/** Chips hinter den Einträgen: USt-Status, DI-Verbrauch, wartende Konten. */
function useChips(): Record<string, { text: string; ton: ChipTon } | 'benutzer' | undefined> {
  const { data: einstellungen } = useEinstellungen()
  const { data: di } = useDiVerbrauch()
  const { istAdmin } = useRolle()
  const ust = einstellungen?.ust.status
  return {
    umsatzsteuer: ust && { text: ustStatusText[ust], ton: ust === 'Unbekannt' ? 'grau' : 'blau' },
    'document-intelligence': di && {
      text: `${di.prozent} %`,
      ton: di.prozent >= (einstellungen?.di.warnungAbProzent ?? 80) ? 'orange' : 'grau',
    },
    benutzer: istAdmin ? 'benutzer' : undefined,
  }
}

/** Bereichsauswahl unter 1024 px: Auswahlliste über dem Inhalt. */
export function EinstellungenAuswahl() {
  const navigate = useNavigate()
  const pfadname = useRouterState({ select: (s) => s.location.pathname })
  const aktiv = pfadname.split('/')[2] ?? ''

  return (
    <label className="block lg:hidden">
      <span className="label">Bereich</span>
      <select
        className="eingabe"
        value={aktiv}
        onChange={(e) => void navigate({ to: `/einstellungen/${e.target.value}` })}
      >
        {bereichsGruppen.map((g) => (
          <optgroup key={g.label} label={g.label}>
            {g.bereiche.map((b) => (
              <option key={b.pfad} value={b.pfad}>
                {b.label}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
    </label>
  )
}

/**
 * Bereichsnavigation ab 1024 px (Einstellungen.dc.html): weiße Spalte zwischen Seitenleiste und Kopfleiste,
 * über die volle Höhe, mit der Seitenüberschrift oben.
 */
export function EinstellungenNavigation() {
  const chips = useChips()

  return (
    <nav
      aria-label="Einstellungsbereiche"
      className="flex min-w-[220px] flex-[0_1_264px] flex-col gap-[18px] border-r border-rand bg-karte px-3.5 py-7"
    >
      <h1 className="m-0 px-2.5 text-[22px] font-semibold text-text">Einstellungen</h1>
      {bereichsGruppen.map((g) => (
        <div key={g.label} role="group" aria-label={g.label} className="flex flex-col gap-0.5">
          <span className="px-2.5 pb-1 text-[11px] font-semibold tracking-[0.08em] text-gedaempft uppercase">
            {g.label}
          </span>
          {g.bereiche.map((b) => {
            const chip = chips[b.pfad]
            return (
              <Link
                key={b.pfad}
                to={`/einstellungen/${b.pfad}`}
                className="flex h-[38px] items-center gap-2.5 rounded-lg px-2.5 text-sm font-medium text-text no-underline hover:bg-hover data-[status=active]:bg-blau-flaeche data-[status=active]:text-akzent-text"
              >
                <Icon name={b.icon} />
                <span className="flex-1">{b.label}</span>
                {chip === 'benutzer' ? <WartendeKonten /> : chip && <Chip ton={chip.ton}>{chip.text}</Chip>}
              </Link>
            )
          })}
        </div>
      ))}
    </nav>
  )
}
