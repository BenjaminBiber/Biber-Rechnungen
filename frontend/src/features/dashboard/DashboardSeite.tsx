import { Link } from '@tanstack/react-router'
import { useDashboard } from '@/api/dashboard'
import type { Dashboard } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Seitenkopf } from '@/components/Seitenkopf'
import { formatDatum } from '@/lib/format'
import { DiVerbrauchKarte } from './DiVerbrauchKarte'
import { type DashboardZeitraum, Skelett } from './gemeinsam'
import { Kennzahlen } from './Kennzahlen'
import { KostenProMonat } from './KostenProMonat'
import { OffenKarte } from './OffenKarte'
import { PreisaenderungenKarte } from './PreisaenderungenKarte'
import { SystemKarte } from './SystemKarte'
import { TopKorrespondenten } from './TopKorrespondenten'
import { VorsteuerKarte } from './VorsteuerKarte'
import { ZuPruefenKarte } from './ZuPruefenKarte'

const zeitraeume: DashboardZeitraum[] = ['Monat', 'Quartal', 'Jahr']

const iso = (d: Date) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

function istLeer(d: Dashboard): boolean {
  return (
    d.kostenJahr.anzahl === 0 &&
    d.offen.anzahl === 0 &&
    d.inbox.anzahl === 0 &&
    d.zuPruefen.anzahl === 0 &&
    d.topKorrespondenten.length === 0
  )
}

/** Dashboard mit Zeitraum- und Jahreswahl; `onWahl` schreibt die Auswahl in die URL. */
export function DashboardSeite({
  zeitraum,
  jahr,
  onWahl,
  heute = new Date(),
}: {
  zeitraum: DashboardZeitraum
  jahr: number
  onWahl: (wahl: { zeitraum?: DashboardZeitraum; jahr?: number }) => void
  heute?: Date
}) {
  const dashboard = useDashboard({ zeitraum, jahr })
  const jahre = Array.from({ length: 5 }, (_, i) => heute.getFullYear() - i)

  return (
    <div className="flex flex-col gap-5">
      <Seitenkopf
        titel="Dashboard"
        untertitel={`Stand ${formatDatum(iso(heute))} · Zeitbasis: Rechnungsdatum · alle Beträge brutto in EUR`}
        aktionen={
          <>
            <div className="segment" role="group" aria-label="Zeitraum">
              {zeitraeume.map((z) => (
                <button key={z} type="button" aria-pressed={z === zeitraum} onClick={() => onWahl({ zeitraum: z })}>
                  {z}
                </button>
              ))}
            </div>
            <label className="btn relative">
              <Icon name="calendar_month" />
              <select
                aria-label="Jahr"
                className="cursor-pointer appearance-none border-0 bg-transparent pr-5 font-[inherit] text-inherit outline-none"
                value={jahr}
                onChange={(e) => onWahl({ jahr: Number(e.target.value) })}
              >
                {(jahre.includes(jahr) ? jahre : [jahr, ...jahre]).map((j) => (
                  <option key={j} value={j}>
                    {j}
                  </option>
                ))}
              </select>
              <span className="pointer-events-none absolute right-2.5">
                <Icon name="expand_more" />
              </span>
            </label>
          </>
        }
      />

      {dashboard.isPending && (
        <div className="flex flex-col gap-5" aria-busy="true" aria-label="Dashboard wird geladen">
          <div className="grid gap-3.5" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(190px, 1fr))' }}>
            {Array.from({ length: 6 }, (_, i) => (
              <Skelett key={i} hoehe={104} />
            ))}
          </div>
          <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(330px, 1fr))' }}>
            {Array.from({ length: 3 }, (_, i) => (
              <Skelett key={i} hoehe={280} />
            ))}
          </div>
        </div>
      )}

      {dashboard.isError && (
        <div role="alert" className="karte flex flex-col items-start gap-3 p-5">
          <p className="m-0 text-sm text-[var(--color-rot-text)]">Das Dashboard konnte nicht geladen werden.</p>
          <button type="button" className="btn" onClick={() => void dashboard.refetch()}>
            <Icon name="refresh" />
            Erneut versuchen
          </button>
        </div>
      )}

      {dashboard.data && istLeer(dashboard.data) && (
        <div className="karte flex flex-col items-start gap-3 p-5">
          <p className="m-0 text-sm">Noch keine Belege – lade den ersten hoch</p>
          <Link to="/hochladen" className="btn btn-primaer">
            <Icon name="upload" />
            Hochladen
          </Link>
        </div>
      )}

      {dashboard.data && (
        <>
          <Kennzahlen daten={dashboard.data} zeitraum={zeitraum} jahr={jahr} heute={heute} />
          <div className="flex flex-wrap gap-5">
            <KostenProMonat monate={dashboard.data.kostenProMonat} jahr={jahr} />
            <TopKorrespondenten eintraege={dashboard.data.topKorrespondenten} jahr={jahr} />
          </div>
          <div
            className="grid gap-5"
            style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(min(330px, 100%), 1fr))' }}
          >
            <ZuPruefenKarte anzahl={dashboard.data.zuPruefen.anzahl} eintraege={dashboard.data.zuPruefenListe} />
            <OffenKarte
              offen={dashboard.data.offen}
              ueberfaellig={dashboard.data.ueberfaellig}
              eintraege={dashboard.data.offeneBelege}
            />
            <SystemKarte />
            <PreisaenderungenKarte eintraege={dashboard.data.preisaenderungen} />
            <VorsteuerKarte vorsteuer={dashboard.data.vorsteuer} jahr={jahr} />
            <DiVerbrauchKarte verbrauch={dashboard.data.diVerbrauch} />
          </div>
        </>
      )}
    </div>
  )
}
