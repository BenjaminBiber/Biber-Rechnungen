import { useDiFortsetzen, useDiVerbindungTesten, useDiVerbrauch } from '@/api/einstellungen'
import type { DiEinstellungen, DiVerbrauch } from '@/api/typen'
import { useRolle } from '@/components/shell/useRolle'
import { formatZahl } from '@/lib/format'
import { Bereich } from '../Bereich'
import { EinstellungsFormular, type Feldfehler } from '../EinstellungsFormular'
import { AktionsKnopf, NurLesenFeld, Raster, ZahlFeld, ganzzahl } from './gemeinsam'
import { NeuAuslesenKarte } from './neuAuslesen'

type DiFormular = Record<keyof DiEinstellungen, string>

/** „2026-11“ → „November 2026“. */
const monatText = (monat: string) => {
  const [jahr, m] = monat.split('-').map(Number)
  return new Date(jahr, m - 1, 1).toLocaleDateString('de-DE', { month: 'long', year: 'numeric' })
}

/** Balkenfarbe: indigo, ab der Warnschwelle orange, ab 100 % rot. */
export function balkenFarbe(prozent: number, warnungAbProzent: number): 'akzent' | 'orange' | 'rot' {
  if (prozent >= 100) return 'rot'
  return prozent >= warnungAbProzent ? 'orange' : 'akzent'
}

const farbWert = { akzent: 'var(--color-akzent)', orange: 'var(--color-orange-text)', rot: 'var(--color-gefahr)' }

function Verbrauchskarte({ verbrauch, warnungAbProzent }: { verbrauch: DiVerbrauch; warnungAbProzent: number }) {
  const farbe = balkenFarbe(verbrauch.prozent, warnungAbProzent)
  return (
    <section aria-label="Verbrauch" className="flex flex-col gap-2 rounded-[10px] bg-flaeche p-4">
      <div className="flex justify-between text-sm">
        <strong>{monatText(verbrauch.monat)}</strong>
        <span className="num">
          {formatZahl(verbrauch.seiten, 0)} / {formatZahl(verbrauch.limit, 0)} Seiten · {verbrauch.prozent} %
        </span>
      </div>
      <div
        className="h-2.5 rounded-[5px] bg-linie"
        role="meter"
        aria-label="Verbrauch Document Intelligence"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.min(100, verbrauch.prozent)}
      >
        <span
          className="block h-2.5 rounded-[5px]"
          data-testid="di-verbrauch-balken"
          data-farbe={farbe}
          style={{ width: `${Math.min(100, verbrauch.prozent)}%`, background: farbWert[farbe] }}
        />
      </div>
      <span className="gedaempft text-[13px]">
        {verbrauch.pausiert
          ? `Warteschlange: pausiert · ${verbrauch.wartend} wartend`
          : `Warteschlange: aktiv · ${verbrauch.wartend} wartend · bei 100 % pausiert sie bis zum Monatswechsel oder bis zur Limit-Erhöhung`}
      </span>
    </section>
  )
}

function validieren(f: DiFormular): Feldfehler {
  const fehler: Feldfehler = {}
  if (ganzzahl(f.monatslimit, 1) === undefined) fehler.monatslimit = 'Ganze Zahl ab 1 eingeben.'
  if (ganzzahl(f.warnungAbProzent, 1, 100) === undefined)
    fehler.warnungAbProzent = 'Ganze Zahl zwischen 1 und 100 eingeben.'
  if (ganzzahl(f.maxSeitenJeBeleg, 1) === undefined) fehler.maxSeitenJeBeleg = 'Ganze Zahl ab 1 eingeben.'
  return fehler
}

const zuFormular = (d: DiEinstellungen): DiFormular => ({
  monatslimit: String(d.monatslimit),
  warnungAbProzent: String(d.warnungAbProzent),
  maxSeitenJeBeleg: String(d.maxSeitenJeBeleg),
})

const zuWerten = (f: DiFormular): DiEinstellungen => ({
  monatslimit: Number(f.monatslimit),
  warnungAbProzent: Number(f.warnungAbProzent),
  maxSeitenJeBeleg: Number(f.maxSeitenJeBeleg),
})

/** Bereich „Document Intelligence“: Verbrauch, Limits, Verbindung, Warteschlange und (Admin) alle Belege neu auslesen. */
export function DocumentIntelligenceBereich() {
  const { data: verbrauch } = useDiVerbrauch()
  const { istAdmin } = useRolle()
  const testen = useDiVerbindungTesten()
  const fortsetzen = useDiFortsetzen()

  return (
    <Bereich titel="Document Intelligence">
      {(e) => (
        <>
          {verbrauch && <Verbrauchskarte verbrauch={verbrauch} warnungAbProzent={e.di.warnungAbProzent} />}
          {!e.umgebung.diKonfiguriert && (
            <p role="note" className="m-0 rounded-[10px] bg-flaeche px-3.5 py-3 text-sm text-gedaempft-stark">
              Document Intelligence ist nicht eingerichtet (.env). Belege ohne E-Rechnung werden manuell erfasst.
            </p>
          )}
          <EinstellungsFormular
            gruppe="di"
            titel="Document Intelligence"
            wert={zuFormular(e.di)}
            validieren={validieren}
            zuWerten={zuWerten}
          >
            {({ wert, setze, fehler }) => (
              <Raster>
                <ZahlFeld
                  id="di-limit"
                  label="Monatslimit (Seiten)"
                  wert={wert.monatslimit}
                  fehler={fehler.monatslimit}
                  onAendern={(v) => setze({ monatslimit: v })}
                />
                <ZahlFeld
                  id="di-warnung"
                  label="Benachrichtigen ab"
                  einheit="%"
                  wert={wert.warnungAbProzent}
                  fehler={fehler.warnungAbProzent}
                  onAendern={(v) => setze({ warnungAbProzent: v })}
                />
                <ZahlFeld
                  id="di-max-seiten"
                  label="Max. Seiten je Beleg"
                  hinweis="Längere Dokumente werden nicht ausgelesen, sondern „zu prüfen“."
                  wert={wert.maxSeitenJeBeleg}
                  fehler={fehler.maxSeitenJeBeleg}
                  onAendern={(v) => setze({ maxSeitenJeBeleg: v })}
                />
                <NurLesenFeld
                  id="di-wiederholungen"
                  label="Wiederholungen bei Fehler"
                  wert="5 Versuche über ca. 1 Std."
                />
                <NurLesenFeld id="di-endpunkt" label="Endpunkt (.env)" wert={e.umgebung.diEndpunkt ?? '–'} mono />
                <NurLesenFeld id="di-modell" label="Modell" wert={e.umgebung.diModell ?? 'prebuilt-invoice'} mono />
              </Raster>
            )}
          </EinstellungsFormular>
          {istAdmin && (
            <div className="flex flex-wrap gap-2">
              <AktionsKnopf aktion={testen}>Verbindung testen</AktionsKnopf>
              <AktionsKnopf aktion={fortsetzen} disabled={!verbrauch || verbrauch.wartend === 0}>
                Warteschlange jetzt fortsetzen
              </AktionsKnopf>
            </div>
          )}
          {istAdmin && <NeuAuslesenKarte verbrauch={verbrauch} diKonfiguriert={e.umgebung.diKonfiguriert} />}
        </>
      )}
    </Bereich>
  )
}
