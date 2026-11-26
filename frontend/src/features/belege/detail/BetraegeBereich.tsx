import { useId } from 'react'
import { Icon } from '@/components/Icon'
import { formatZahl } from '@/lib/format'
import { betragEingabe, parseBetrag, parseSatz, STEUER_TOLERANZ } from './gemeinsam'
import { neuerSatz, type FeldFehler, type Formular } from './formular'

type Zeile = Formular['steuerzeilen'][number]
type ZeilenFeld = keyof Zeile
type KopfFeld = 'netto' | 'steuer' | 'brutto'

const euro = (wert: number) => `${formatZahl(wert, 2)} €`
const cent = (wert: number) => Math.round(wert * 100) / 100

// Steuersatz | Netto | + Steuer | = Brutto | Prüfung/Aktionen – feste Anteile, passt auch auf Laptop-Breiten.
const raster = 'grid grid-cols-[110px_repeat(3,minmax(0,1fr))_68px] items-center gap-x-2.5 gap-y-1.5'
const feldRahmen =
  'flex h-[34px] items-center gap-1 rounded-[7px] border border-eingabe-rand bg-karte px-2.5 text-[13px] text-gedaempft focus-within:border-akzent focus-within:shadow-[0_0_0_3px_var(--color-blau-flaeche)]'
const feldEingabe =
  'w-full min-w-0 flex-1 border-0 bg-transparent p-0 font-sans text-sm text-text tabular-nums outline-none'
const warnRahmen = 'border-orange-text/60 bg-orange-flaeche/40'
const op = 'mr-1 inline-block w-4 text-center font-bold text-akzent-text'

/** Prüfung einer Steuerzeile: Netto × Satz gegen die eingetragene Steuer. */
function zeilenPruefung(zeile: Zeile) {
  const satz = parseSatz(zeile.satz)
  const netto = parseBetrag(zeile.netto)
  const steuer = parseBetrag(zeile.steuer)
  if (satz === null || netto === null || steuer === null) return { netto, steuer, satz, erwartet: null, stimmt: null }
  const erwartet = Math.round(netto * satz) / 100
  return { netto, steuer, satz, erwartet, stimmt: Math.abs(erwartet - steuer) <= STEUER_TOLERANZ + 1e-9 }
}

/** Summe einer Spalte gegen den Wert laut Rechnung (Kopfwert des Belegs). */
function kopfVergleich(label: string, rechnung: string, summe: number | null) {
  if (summe === null) return null
  const wert = rechnung.trim() ? parseBetrag(rechnung) : null
  if (wert === null) return { stimmt: false, titel: `${label} fehlt`, text: `Laut Rechnung ist kein Wert erfasst.` }
  const differenz = cent(wert - summe)
  if (Math.abs(differenz) <= STEUER_TOLERANZ + 1e-9)
    return { stimmt: true, titel: `${label} stimmt`, text: `Entspricht dem Wert laut Rechnung (${euro(wert)}).` }
  return {
    stimmt: false,
    titel: `${label} weicht ab`,
    text: `Laut Rechnung ${euro(wert)}, das sind ${euro(Math.abs(differenz))} ${differenz > 0 ? 'mehr' : 'weniger'} als die Summe der Steuerzeilen.`,
  }
}

/**
 * Bereich „Beträge“ im Tab „Daten“: Steuerzeilen je Steuersatz (Netto + Steuer = Brutto) und ihre Summe, verglichen
 * mit Netto, USt und Brutto laut Rechnung. Die Rechnungswerte lassen sich über „Rechnungswert auf … setzen“ an die
 * Summe angleichen.
 */
export function BetraegeBereich({
  werte,
  fehler,
  nurLesen,
  onZeile,
  onZeilen,
  onKopf,
}: {
  werte: Formular
  fehler: FeldFehler
  nurLesen: boolean
  onZeile: (index: number, feld: ZeilenFeld, wert: string) => void
  /** Neue Zeilenliste; `sofort` speichert gleich (Entfernen). */
  onZeilen: (zeilen: Zeile[], sofort: boolean) => void
  /** Setzt einen Rechnungswert („Rechnungswert auf … setzen“) und speichert sofort. */
  onKopf: (feld: KopfFeld, wert: string) => void
}) {
  const id = useId()
  const zeilen = werte.steuerzeilen.map(zeilenPruefung)
  // Summen nur, wenn alle Werte der Spalte lesbar sind – sonst wäre der Vergleich irreführend.
  const summe = (werteDerSpalte: (number | null)[]) =>
    werteDerSpalte.length > 0 && werteDerSpalte.every((w) => w !== null)
      ? cent(werteDerSpalte.reduce<number>((s, w) => s + w!, 0))
      : null
  const summeNetto = summe(zeilen.map((z) => z.netto))
  const summeSteuer = summe(zeilen.map((z) => z.steuer))
  const summeBrutto = summeNetto !== null && summeSteuer !== null ? cent(summeNetto + summeSteuer) : null
  const kopf = [
    { feld: 'netto' as const, label: 'Netto', summe: summeNetto },
    { feld: 'steuer' as const, label: 'USt', summe: summeSteuer },
    { feld: 'brutto' as const, label: 'Brutto', summe: summeBrutto },
  ].map((k) => ({ ...k, vergleich: kopfVergleich(k.label, werte[k.feld], k.summe) }))

  const abweichungen =
    zeilen.filter((z) => z.stimmt === false).length + kopf.filter((k) => k.vergleich?.stimmt === false).length
  const hatZeilen = zeilen.length > 0
  // Fehler ohne eigenes Eingabefeld: die Rechnungswerte (nur über „Rechnungswert auf … setzen“ änderbar) und die Tabelle.
  const bereichsFehler = [
    ...kopf.flatMap((k) => (fehler[k.feld] ? [`${k.label} laut Rechnung: ${fehler[k.feld]}`] : [])),
    ...(fehler.steuerzeilen ? [fehler.steuerzeilen] : []),
  ]

  return (
    <div>
      <div className="mb-2.5 flex items-baseline justify-between gap-3">
        <h2 className="m-0 text-[15px] font-semibold">Beträge</h2>
        {hatZeilen && (
          <span
            className={`inline-flex items-center gap-1.5 text-[13px] font-medium ${abweichungen === 0 ? 'text-gruen-text' : 'text-orange-text'}`}
          >
            <Icon name={abweichungen === 0 ? 'check_circle' : 'error'} className="align-middle text-[18px]" />
            {abweichungen === 0
              ? 'Alles stimmig'
              : `${abweichungen} ${abweichungen === 1 ? 'Abweichung' : 'Abweichungen'}`}
          </span>
        )}
      </div>

      <div role="table" aria-label="Beträge nach Steuersatz" className="flex flex-col">
        <div role="row" className={`${raster} border-b border-rand pb-1.5 text-xs font-semibold text-gedaempft-stark`}>
          <span role="columnheader">Steuersatz</span>
          <span role="columnheader" className="text-right">
            Netto
          </span>
          <span role="columnheader" className="text-right">
            <span className={op} aria-hidden="true">
              +
            </span>
            Steuer
          </span>
          <span role="columnheader" className="text-right">
            <span className={op} aria-hidden="true">
              =
            </span>
            Brutto
          </span>
          <span role="columnheader">
            <span className="sr-only">Prüfung und Aktionen</span>
          </span>
        </div>

        {werte.steuerzeilen.map((zeile, i) => {
          const pruefung = zeilen[i]
          const nr = i + 1
          const brutto =
            pruefung.netto !== null && pruefung.steuer !== null ? cent(pruefung.netto + pruefung.steuer) : null
          const pruefText =
            pruefung.stimmt === null
              ? null
              : pruefung.stimmt
                ? `Steuer passt: ${formatZahl(pruefung.satz!)} % von ${euro(pruefung.netto!)} = ${euro(pruefung.erwartet!)}`
                : 'Steuer passt nicht zum Satz'
          const hinweisId = `${id}-${i}-hinweis`
          const zelle = (feld: ZeilenFeld, label: string, warnen = false) => {
            const schluessel = `steuerzeilen.${i}.${feld}`
            const fehlerId = `${id}-${i}-${feld}-fehler`
            const beschreibung = fehler[schluessel] ? fehlerId : warnen ? hinweisId : undefined
            if (nurLesen)
              return (
                <span role="cell" className={`px-2.5 text-sm tabular-nums ${feld === 'satz' ? '' : 'text-right'}`}>
                  {feld === 'satz' ? `${zeile.satz} %` : `${zeile[feld]} €`}
                </span>
              )
            return (
              <div role="cell" className="min-w-0">
                <div className={`${feldRahmen} ${fehler[schluessel] ? 'border-rot-text' : warnen ? warnRahmen : ''}`}>
                  <input
                    className={`${feldEingabe} ${feld === 'satz' ? '' : 'text-right'}`}
                    name={schluessel}
                    aria-label={`${label} Steuerzeile ${nr}`}
                    inputMode="decimal"
                    value={zeile[feld]}
                    aria-invalid={fehler[schluessel] ? true : undefined}
                    aria-describedby={beschreibung}
                    onChange={(e) => onZeile(i, feld, e.target.value)}
                  />
                  {feld === 'satz' && <span aria-hidden="true">%</span>}
                </div>
                {fehler[schluessel] && (
                  <span id={fehlerId} role="alert" className="hinweis text-rot-text">
                    {fehler[schluessel]}
                  </span>
                )}
              </div>
            )
          }
          return (
            <div key={i} role="row" className={`${raster} border-b border-linie py-1.5`}>
              {zelle('satz', 'Steuersatz')}
              {zelle('netto', 'Netto')}
              {zelle('steuer', 'Steuer', pruefung.stimmt === false)}
              <span
                role="cell"
                className="flex h-[34px] items-center justify-end rounded-[7px] bg-flaeche px-2.5 text-sm text-gedaempft-stark tabular-nums"
                title="Netto + Steuer, wird berechnet"
              >
                {brutto === null ? '—' : formatZahl(brutto, 2)}
              </span>
              <span role="cell" className="flex items-center justify-end gap-0.5">
                {pruefText && (
                  <Icon
                    name={pruefung.stimmt ? 'check_circle' : 'error'}
                    label={pruefText}
                    className={`text-[20px] ${pruefung.stimmt ? 'text-gruen-text' : 'text-orange-text'}`}
                  />
                )}
                {!nurLesen && (
                  <button
                    type="button"
                    className="inline-flex size-8 cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-gedaempft-stark hover:bg-hover"
                    aria-label={`Steuerzeile ${nr} entfernen`}
                    title="Zeile entfernen"
                    onClick={() =>
                      onZeilen(
                        werte.steuerzeilen.filter((_, j) => j !== i),
                        true,
                      )
                    }
                  >
                    <Icon name="delete" className="text-[19px]" />
                  </button>
                )}
              </span>
              {pruefung.stimmt === false && (
                <span id={hinweisId} className="col-[2/6] -mt-0.5 text-xs text-orange-text">
                  {`${formatZahl(pruefung.satz!)} % von ${euro(pruefung.netto!)} wären ${euro(pruefung.erwartet!)} – eingetragen sind ${euro(pruefung.steuer!)} (Differenz ${euro(cent(Math.abs(pruefung.erwartet! - pruefung.steuer!)))})`}
                </span>
              )}
              {fehler[`steuerzeilen.${i}`] && (
                <span role="alert" className="col-[1/6] -mt-0.5 text-xs text-rot-text">
                  {`Steuerzeile ${nr}: ${fehler[`steuerzeilen.${i}`]}`}
                </span>
              )}
            </div>
          )
        })}

        {!hatZeilen && (
          <div role="row" className="border-b border-linie py-2.5 text-[13px] text-gedaempft">
            <span role="cell">Keine Aufschlüsselung nach Steuersätzen</span>
          </div>
        )}

        {!nurLesen && (
          <div className={`${raster} pt-1 pb-1.5`}>
            <button
              type="button"
              className="col-[1/3] cursor-pointer justify-self-start border-0 bg-transparent p-0 text-[13px] font-medium text-akzent-text"
              onClick={() =>
                onZeilen([...werte.steuerzeilen, { satz: neuerSatz(werte.steuerzeilen), netto: '', steuer: '' }], false)
              }
            >
              + Steuerzeile
            </button>
          </div>
        )}

        <div role="row" className={`${raster} border-t-2 border-schalter-aus py-2.5 text-sm font-semibold`}>
          <span role="rowheader">{hatZeilen ? 'Summe' : 'Laut Rechnung'}</span>
          {kopf.map((k) => (
            <span role="cell" key={k.feld} className="flex items-center justify-end gap-1.5 pr-2.5">
              <span className="tabular-nums">
                {hatZeilen
                  ? k.summe === null
                    ? '—'
                    : euro(k.summe)
                  : werte[k.feld].trim()
                    ? `${werte[k.feld]} €`
                    : '—'}
              </span>
              {k.vergleich && (
                <span className="group/tipp relative inline-flex">
                  <button
                    type="button"
                    className="inline-flex cursor-help rounded-full border-0 bg-transparent p-0 focus-visible:shadow-[0_0_0_3px_var(--color-blau-flaeche)] focus-visible:outline-none"
                    aria-label={`${k.vergleich.titel}: ${k.vergleich.text}`}
                  >
                    <Icon
                      name={k.vergleich.stimmt ? 'check_circle' : 'error'}
                      className={`icon-gefuellt text-[18px] ${k.vergleich.stimmt ? 'text-gruen-text' : 'text-orange-text'}`}
                    />
                  </button>
                  {/* Hover-/Fokus-Karte; die unsichtbare Brücke (::after) hält sie offen, während die Maus hinüberwandert. */}
                  <span
                    role="tooltip"
                    className="absolute right-[-8px] bottom-[calc(100%+8px)] z-30 hidden w-60 flex-col gap-1.5 rounded-[9px] border border-rand bg-karte px-3 py-2.5 text-left text-xs leading-[1.45] font-normal whitespace-normal text-gedaempft-stark shadow-popover group-focus-within/tipp:flex group-hover/tipp:flex after:absolute after:inset-x-0 after:top-full after:h-2.5 after:content-['']"
                  >
                    <span
                      className={`flex items-center gap-1.5 font-semibold ${k.vergleich.stimmt ? 'text-gruen-text' : 'text-orange-text'}`}
                    >
                      {k.vergleich.titel}
                    </span>
                    <span>{k.vergleich.text}</span>
                    {!k.vergleich.stimmt && !nurLesen && k.summe !== null && (
                      <button
                        type="button"
                        className="cursor-pointer self-start border-0 bg-transparent p-0 text-xs font-medium text-akzent-text underline"
                        onClick={() => onKopf(k.feld, betragEingabe(k.summe))}
                      >
                        {`Rechnungswert auf ${euro(k.summe)} setzen`}
                      </button>
                    )}
                  </span>
                </span>
              )}
            </span>
          ))}
          <span role="cell" />
        </div>
      </div>
      {bereichsFehler.map((text) => (
        <span key={text} role="alert" className="hinweis text-rot-text">
          {text}
        </span>
      ))}
    </div>
  )
}
