import type { PruefEinstellungen, PruefungArt } from '@/api/typen'
import { Bereich } from './Bereich'
import { EinstellungsFormular, type Feldfehler } from './EinstellungsFormular'
import { Schalter } from './Schalter'
import { Zeile } from './Zeile'

type Schwelle = 'summenToleranz' | 'maxAlterJahre' | 'kleinbetragGrenze' | 'konfidenzSchwelle'

/** Formularzustand: Schwellen als Text im deutschen Zahlenformat. */
type PruefFormular = { aktiv: Record<string, boolean> } & Record<Schwelle, string>

type Pruefung = {
  art: PruefungArt
  name: string
  detail: string
  schwelle?: Schwelle
  einheit?: string
}

/** Die neun Prüfungen in der Reihenfolge des Designs. */
const pruefungen: Pruefung[] = [
  {
    art: 'Positionssumme',
    name: 'Summe der Positionen = Netto/Brutto',
    detail: 'Abweichung erlaubt bis',
    schwelle: 'summenToleranz',
    einheit: '€',
  },
  {
    art: 'NettoSteuerBrutto',
    name: 'Netto + USt = Brutto je Steuersatz',
    detail: 'Abweichung erlaubt bis',
    schwelle: 'summenToleranz',
    einheit: '€',
  },
  { art: 'Pflichtfelder', name: 'Pflichtfelder', detail: 'Korrespondent, Datum, Rechnungsnummer, Gesamtbetrag' },
  {
    art: 'Datum',
    name: 'Datum plausibel',
    detail: 'nicht in der Zukunft und nicht älter als',
    schwelle: 'maxAlterJahre',
    einheit: 'Jahre',
  },
  {
    art: 'Paragraph14',
    name: 'Angaben nach § 14 UStG',
    detail: 'reduziert bei Kleinbetragsrechnungen bis',
    schwelle: 'kleinbetragGrenze',
    einheit: '€',
  },
  { art: 'Dublette', name: 'Dublettenverdacht', detail: 'Korrespondent + Rechnungsnr. + Betrag' },
  { art: 'NeuerKorrespondent', name: 'Neuer Korrespondent', detail: 'beim ersten Beleg eines Lieferanten' },
  {
    art: 'Konfidenz',
    name: 'Erkennungssicherheit (Konfidenz)',
    detail: 'Document Intelligence unter',
    schwelle: 'konfidenzSchwelle',
  },
  { art: 'Kosit', name: 'KoSIT-Validierung', detail: 'nur für XRechnung / ZUGFeRD' },
]

const zahlFormat = (wert: number, stellen: number) =>
  wert.toLocaleString('de-DE', {
    minimumFractionDigits: stellen,
    maximumFractionDigits: Math.max(stellen, 4),
    useGrouping: false,
  })

/** Liest „0,85“ oder „250“; `undefined`, wenn es keine Zahl ist. */
export function zahlLesen(text: string): number | undefined {
  const t = text.trim()
  if (!/^\d+(,\d+)?$/.test(t)) return undefined
  return Number(t.replace(',', '.'))
}

const grenzen: Record<Schwelle, { min: number; max?: number; ganz?: boolean; meldung: string }> = {
  summenToleranz: { min: 0, max: 100, meldung: 'Betrag zwischen 0 und 100 € eingeben.' },
  maxAlterJahre: { min: 1, max: 30, ganz: true, meldung: 'Ganze Zahl zwischen 1 und 30 eingeben.' },
  kleinbetragGrenze: { min: 0, max: 100000, meldung: 'Betrag ab 0 € eingeben.' },
  konfidenzSchwelle: { min: 0, max: 1, meldung: 'Wert zwischen 0 und 1 eingeben.' },
}

function validieren(f: PruefFormular): Feldfehler {
  const fehler: Feldfehler = {}
  for (const [schwelle, g] of Object.entries(grenzen) as [Schwelle, (typeof grenzen)[Schwelle]][]) {
    const zahl = zahlLesen(f[schwelle])
    if (
      zahl === undefined ||
      zahl < g.min ||
      (g.max !== undefined && zahl > g.max) ||
      (g.ganz && !Number.isInteger(zahl))
    )
      fehler[schwelle] = g.meldung
  }
  return fehler
}

const zuFormular = (p: PruefEinstellungen): PruefFormular => ({
  aktiv: Object.fromEntries(pruefungen.map((x) => [x.art, p.aktiv[x.art] ?? true])),
  summenToleranz: zahlFormat(p.summenToleranz, 2),
  maxAlterJahre: zahlFormat(p.maxAlterJahre, 0),
  kleinbetragGrenze: zahlFormat(p.kleinbetragGrenze, 0),
  konfidenzSchwelle: zahlFormat(p.konfidenzSchwelle, 2),
})

const zuWerten = (f: PruefFormular): PruefEinstellungen => ({
  aktiv: f.aktiv,
  summenToleranz: zahlLesen(f.summenToleranz)!,
  maxAlterJahre: zahlLesen(f.maxAlterJahre)!,
  kleinbetragGrenze: zahlLesen(f.kleinbetragGrenze)!,
  konfidenzSchwelle: zahlLesen(f.konfidenzSchwelle)!,
})

/** Bereich „Prüfungen“: neun Prüfungen mit Schalter und – wo vorhanden – Schwelle. */
export function PruefungenBereich() {
  return (
    <Bereich titel="Prüfungen & Schwellen">
      {(e) => (
        <EinstellungsFormular
          gruppe="pruefungen"
          titel="Prüfungen & Schwellen"
          wert={zuFormular(e.pruefungen)}
          validieren={validieren}
          zuWerten={zuWerten}
        >
          {({ wert, setze, fehler, schreibgeschuetzt }) => (
            <div className="flex flex-col">
              <p className="gedaempft m-0 mb-2.5 text-sm">
                Schlägt eine aktive Prüfung fehl, bekommt der Beleg „zu prüfen“ mit Begründung.
              </p>
              {pruefungen.map((p) => (
                <Zeile key={p.art} titel={p.name} beschreibung={p.detail} fehler={p.schwelle && fehler[p.schwelle]}>
                  {p.schwelle && (
                    <span className="flex items-center gap-1.5 text-[13px]">
                      <input
                        className="eingabe num w-[90px]"
                        inputMode="decimal"
                        aria-label={`${p.name} Schwelle`}
                        aria-invalid={!!fehler[p.schwelle]}
                        value={wert[p.schwelle]}
                        onChange={(ev) => setze({ [p.schwelle!]: ev.target.value })}
                      />
                      {p.einheit && <span className="gedaempft">{p.einheit}</span>}
                    </span>
                  )}
                  <Schalter
                    label={p.name}
                    an={wert.aktiv[p.art]}
                    disabled={schreibgeschuetzt}
                    onAendern={(an) => setze({ aktiv: { ...wert.aktiv, [p.art]: an } })}
                  />
                </Zeile>
              ))}
            </div>
          )}
        </EinstellungsFormular>
      )}
    </Bereich>
  )
}
