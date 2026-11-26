import { useState } from 'react'
import { useLoeschbar } from '@/api/aufbewahrung'
import type { Loeschbar } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { Button } from '@/components/ui/button'
import { useRolle } from '@/components/shell/useRolle'
import { formatDatum } from '@/lib/format'
import { Bereich } from './Bereich'
import { EinstellungsFormular, type Feldfehler } from './EinstellungsFormular'
import { LoeschDialog } from './LoeschDialog'
import { SkelettListe } from '@/components/ui/skelett'

const MINDEST_JAHRE = 8

const jahreLesen = (text: string) => (/^\d+$/.test(text.trim()) ? Number(text.trim()) : undefined)

function validieren({ jahre }: { jahre: string }): Feldfehler {
  const zahl = jahreLesen(jahre)
  return zahl === undefined || zahl < MINDEST_JAHRE
    ? { jahre: `Die gesetzliche Aufbewahrungsfrist beträgt mindestens ${MINDEST_JAHRE} Jahre.` }
    : {}
}

/** Leerzustand „Keine Belege mit abgelaufener Frist“. */
function Leer({ jahre }: { jahre: number }) {
  const ab = `01.01.${new Date().getFullYear() + jahre + 1}`
  return (
    <div className="flex flex-col items-center gap-1.5 rounded-[10px] border border-dashed border-schalter-aus px-5 py-8 text-center">
      <Icon name="lock_clock" className="text-[36px] text-[#9AA4B5]" />
      <strong>Keine Belege mit abgelaufener Frist</strong>
      <span className="gedaempft text-sm">
        Die ersten Belege werden frühestens am {ab} löschbar. Danach gibt der Admin sie hier frei; die App entfernt die
        Daten, die Datei auf dem WORM-Share löscht der Admin auf dem Host.
      </span>
    </div>
  )
}

/** Löschbare Jahrgänge (nur Admins dürfen sie abfragen und freigeben). */
function LoeschbarListe({ jahre }: { jahre: number }) {
  const { data, isPending } = useLoeschbar()
  const [gewaehlt, setGewaehlt] = useState<Loeschbar>()
  if (isPending) return <SkelettListe zeilen={2} kompakt symbol={false} label="Löschbare Jahrgänge werden geladen" />
  return (
    <>
      {!data?.length ? (
        <Leer jahre={jahre} />
      ) : (
        <ul className="m-0 flex list-none flex-col p-0">
          {data.map((l) => (
            <li key={l.jahr} className="flex flex-wrap items-center justify-between gap-3 border-t border-linie py-3">
              <span className="text-sm">
                {l.anzahl} Belege aus {l.jahr} · Frist endete am {formatDatum(l.fristEnde)}
              </span>
              <Button type="button" variant="gefaehrlich" size="klein" onClick={() => setGewaehlt(l)}>
                Löschung freigeben …
              </Button>
            </li>
          ))}
        </ul>
      )}
      {gewaehlt && <LoeschDialog eintrag={gewaehlt} onSchliessen={() => setGewaehlt(undefined)} />}
    </>
  )
}

/** Bereich „Aufbewahrung“: Löschsperre, berechnete Sperrfrist und freizugebende Jahrgänge. */
export function AufbewahrungBereich() {
  const { istAdmin } = useRolle()
  const jahr = new Date().getFullYear()

  return (
    <Bereich titel="Aufbewahrung & Löschung">
      {(e) => (
        <>
          <EinstellungsFormular
            gruppe="aufbewahrung"
            titel="Aufbewahrung & Löschung"
            wert={{ jahre: String(e.aufbewahrung.jahre) }}
            validieren={validieren}
            zuWerten={({ jahre }) => ({ jahre: jahreLesen(jahre)! })}
          >
            {({ wert, setze, fehler }) => {
              const jahre = jahreLesen(wert.jahre)
              return (
                <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
                  <div>
                    <label className="label" htmlFor="r-jahre">
                      Löschsperre
                    </label>
                    <span className="flex items-center gap-1.5 text-[13px]">
                      <input
                        id="r-jahre"
                        className="eingabe num"
                        inputMode="numeric"
                        aria-invalid={!!fehler.jahre}
                        value={wert.jahre}
                        onChange={(ev) => setze({ jahre: ev.target.value })}
                      />
                      <span className="gedaempft">Jahre</span>
                    </span>
                    <span className="hinweis">ab Ende des Belegjahres (§ 147 AO)</span>
                    {fehler.jahre && (
                      <span role="alert" className="hinweis text-gefahr">
                        {fehler.jahre}
                      </span>
                    )}
                  </div>
                  <div>
                    <label className="label" htmlFor="r-bis">
                      Belege {jahr} gesperrt bis
                    </label>
                    <input
                      id="r-bis"
                      className="eingabe"
                      readOnly
                      value={jahre === undefined ? '–' : `31.12.${jahr + jahre}`}
                    />
                  </div>
                </div>
              )
            }}
          </EinstellungsFormular>
          <div>
            <h3 className="m-0 mb-2 text-[15px] font-semibold text-text">Löschung möglich</h3>
            {istAdmin ? <LoeschbarListe jahre={e.aufbewahrung.jahre} /> : <Leer jahre={e.aufbewahrung.jahre} />}
          </div>
        </>
      )}
    </Bereich>
  )
}
