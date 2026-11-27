import { useDashboard } from '@/api/dashboard'
import { Icon } from '@/components/Icon'
import { Seitenkopf } from '@/components/Seitenkopf'
import { LadeBereich, SkelettKarte, SkelettKennzahlen, SkelettListe } from '@/components/ui/skelett'
import { type DashboardZeitraum } from './gemeinsam'
import { Kennzahlen } from './Kennzahlen'
import { KostenProMonat } from './KostenProMonat'
import { OffenKarte } from './OffenKarte'
import { PreisaenderungenKarte } from './PreisaenderungenKarte'
import { SkontoKarte } from './SkontoKarte'
import { SystemKarte } from './SystemKarte'
import { TopKorrespondenten } from './TopKorrespondenten'
import { VorsteuerKarte } from './VorsteuerKarte'
import { ZuPruefenKarte } from './ZuPruefenKarte'

const zeitraeume: DashboardZeitraum[] = ['Monat', 'Quartal', 'Jahr']

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
    <main className="seite flex flex-col gap-5">
      <Seitenkopf
        titel="Dashboard"
        className="mb-0"
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
        <LadeBereich label="Dashboard wird geladen" className="flex flex-col gap-5">
          <SkelettKennzahlen anzahl={6} hoehe={104} className="sm:grid-cols-3 md:grid-cols-3 2xl:grid-cols-6" />
          <div className="flex flex-wrap gap-5">
            <SkelettKarte hoehe={240} className="flex-[2_1_480px]" />
            <SkelettKarte className="flex-[1_1_300px]">
              <SkelettListe zeilen={5} kompakt label={null} />
            </SkelettKarte>
          </div>
          <div
            className="grid gap-5"
            style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(min(300px, 100%), 1fr))' }}
          >
            {Array.from({ length: 3 }, (_, i) => (
              <SkelettKarte key={i}>
                <SkelettListe zeilen={4} symbol={false} label={null} />
              </SkelettKarte>
            ))}
          </div>
        </LadeBereich>
      )}

      {dashboard.isError && (
        <div role="alert" className="karte flex flex-col items-start gap-3 p-5">
          <p className="m-0 text-sm text-[var(--color-rot-text)]">Das Dashboard konnte nicht geladen werden.</p>
          <button
            type="button"
            className="btn"
            onClick={() => void dashboard.refetch()}
            aria-busy={dashboard.isFetching || undefined}
          >
            <Icon name="refresh" />
            Erneut versuchen
          </button>
        </div>
      )}

      {dashboard.data && (
        // Beim Wechsel von Zeitraum oder Jahr bleiben die alten Zahlen stehen, bis die neuen da sind.
        <>
          <Kennzahlen daten={dashboard.data} zeitraum={zeitraum} jahr={jahr} heute={heute} />
          <div className="flex flex-wrap gap-5">
            <KostenProMonat monate={dashboard.data.kostenProMonat} jahr={jahr} />
            <TopKorrespondenten eintraege={dashboard.data.topKorrespondenten} jahr={jahr} />
          </div>
          <div
            className="grid gap-5"
            style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(min(300px, 100%), 1fr))' }}
          >
            <ZuPruefenKarte anzahl={dashboard.data.zuPruefen.anzahl} eintraege={dashboard.data.zuPruefenListe} />
            <OffenKarte
              offen={dashboard.data.offen}
              ueberfaellig={dashboard.data.ueberfaellig}
              eintraege={dashboard.data.offeneBelege}
            />
            <SkontoKarte skonto={dashboard.data.skonto} heute={heute} />
            <SystemKarte />
            <PreisaenderungenKarte eintraege={dashboard.data.preisaenderungen} />
            <VorsteuerKarte vorsteuer={dashboard.data.vorsteuer} jahr={jahr} />
          </div>
        </>
      )}
    </main>
  )
}
