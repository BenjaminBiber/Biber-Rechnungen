import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { usePositionenErsetzen } from '@/api/belege'
import { useEinstellungen } from '@/api/einstellungen'
import type { BelegDetail, Position, PositionAendern } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { formatEuro, formatZahl } from '@/lib/format'
import { betragEingabe, parseBetrag, parseSatz, satzEingabe, STEUER_TOLERANZ } from '../gemeinsam'

/** Eingabefelder einer Zeile im Bearbeiten-Modus (deutsche Schreibweise). */
type Entwurf = { text: string; menge: string; einheit: string; einzelpreis: string; gesamt: string; steuersatz: string }

const leererEntwurf: Entwurf = { text: '', menge: '1', einheit: '', einzelpreis: '', gesamt: '', steuersatz: '19' }

function entwurfAus(p: Position): Entwurf {
  return {
    text: p.text,
    menge: p.menge === null || p.menge === undefined ? '' : formatZahl(p.menge),
    einheit: p.einheit ?? '',
    einzelpreis: betragEingabe(p.einzelpreis),
    gesamt: betragEingabe(p.gesamt),
    steuersatz: p.steuersatz === null || p.steuersatz === undefined ? '' : satzEingabe(p.steuersatz),
  }
}

/** Gesamt-Vorschlag Menge × Einzelpreis; `null`, wenn eins von beiden fehlt. */
export function gesamtVorschlag(menge: string, einzelpreis: string): string | null {
  const m = parseBetrag(menge)
  const e = parseBetrag(einzelpreis)
  return m === null || e === null ? null : betragEingabe(Math.round(m * e * 100) / 100)
}

function runden(wert: number): number {
  return Math.round(wert * 100) / 100
}

function alsAenderung(p: Position): PositionAendern {
  return {
    nr: p.nr,
    text: p.text,
    menge: p.menge ?? null,
    einheit: p.einheit ?? null,
    einzelpreis: p.einzelpreis ?? null,
    gesamt: p.gesamt,
    steuersatz: p.steuersatz ?? null,
  }
}

function mengeText(p: Position): string {
  if (p.menge === null || p.menge === undefined) return '—'
  return p.einheit ? `${formatZahl(p.menge)} ${p.einheit}` : formatZahl(p.menge)
}

function grundpreisText(p: Position): string {
  if (p.grundpreis === null || p.grundpreis === undefined) return '—'
  return p.grundpreisEinheit ? `${formatEuro(p.grundpreis)}/${p.grundpreisEinheit}` : formatEuro(p.grundpreis)
}

/** Tab „Positionen“: Tabelle, Summenvergleich mit dem Netto und (mit Bearbeiten-Recht) Bearbeitung aller Zeilen. */
export function PositionenTab({ beleg, darfBearbeiten }: { beleg: BelegDetail; darfBearbeiten: boolean }) {
  const toast = useToast()
  const ersetzen = usePositionenErsetzen()
  const { data: einstellungen } = useEinstellungen()
  const toleranz = einstellungen?.pruefungen.summenToleranz ?? STEUER_TOLERANZ
  const [zeilen, setZeilen] = useState<Position[]>(beleg.positionen)
  const [bearbeitet, setBearbeitet] = useState<{ index: number; entwurf: Entwurf; fehler?: string } | null>(null)
  const geaendert = JSON.stringify(zeilen.map(alsAenderung)) !== JSON.stringify(beleg.positionen.map(alsAenderung))

  const summe = runden(zeilen.reduce((s, p) => s + p.gesamt, 0))
  const netto = beleg.netto ?? null
  const differenz = netto === null ? null : runden(Math.abs(netto - summe))
  const abweichung = differenz !== null && differenz > toleranz + 1e-9

  function feldAendern(feld: keyof Entwurf, wert: string) {
    if (!bearbeitet) return
    const entwurf = { ...bearbeitet.entwurf, [feld]: wert }
    if (feld === 'menge' || feld === 'einzelpreis') {
      const vorschlag = gesamtVorschlag(entwurf.menge, entwurf.einzelpreis)
      if (vorschlag !== null) entwurf.gesamt = vorschlag
    }
    setBearbeitet({ ...bearbeitet, entwurf, fehler: undefined })
  }

  function uebernehmen() {
    if (!bearbeitet) return
    const { entwurf, index } = bearbeitet
    const gesamt = parseBetrag(entwurf.gesamt)
    const menge = entwurf.menge.trim() ? parseBetrag(entwurf.menge) : null
    const einzelpreis = entwurf.einzelpreis.trim() ? parseBetrag(entwurf.einzelpreis) : null
    const steuersatz = entwurf.steuersatz.trim() ? parseSatz(entwurf.steuersatz) : null
    if (!entwurf.text.trim()) return setBearbeitet({ ...bearbeitet, fehler: 'Bitte einen Text eingeben.' })
    if (gesamt === null) return setBearbeitet({ ...bearbeitet, fehler: 'Bitte einen gültigen Gesamtbetrag eingeben.' })
    if ((entwurf.menge.trim() && menge === null) || (entwurf.einzelpreis.trim() && einzelpreis === null))
      return setBearbeitet({ ...bearbeitet, fehler: 'Menge oder Einzelpreis ist ungültig.' })
    if (entwurf.steuersatz.trim() && steuersatz === null)
      return setBearbeitet({ ...bearbeitet, fehler: 'Der Steuersatz ist ungültig.' })
    const alt = zeilen[index] as Position | undefined
    const neu: Position = {
      ...(alt ?? { artikel: null, stammartikelId: null, grundpreis: null, grundpreisEinheit: null }),
      nr: alt?.nr ?? index + 1,
      text: entwurf.text.trim(),
      menge,
      einheit: entwurf.einheit.trim() || null,
      einzelpreis,
      gesamt,
      steuersatz,
    }
    setZeilen(index < zeilen.length ? zeilen.map((z, i) => (i === index ? neu : z)) : [...zeilen, neu])
    setBearbeitet(null)
  }

  function loeschen(index: number) {
    setZeilen(zeilen.filter((_, i) => i !== index).map((z, i) => ({ ...z, nr: i + 1 })))
    setBearbeitet(null)
  }

  function speichern() {
    ersetzen.mutate(
      { id: beleg.id, positionen: zeilen.map((z, i) => alsAenderung({ ...z, nr: i + 1 })) },
      {
        onSuccess: (neu) => {
          setZeilen(neu.positionen)
          toast.erfolg('Positionen gespeichert')
        },
        onError: () => toast.fehler('Positionen konnten nicht gespeichert werden', { erneut: speichern }),
      },
    )
  }

  function verwerfen() {
    setZeilen(beleg.positionen)
    setBearbeitet(null)
  }

  const neueZeile = bearbeitet !== null && bearbeitet.index >= zeilen.length

  return (
    <div className="flex flex-col gap-3">
      {/* relative: auch absolut positionierte Kinder (sr-only) bleiben im Scroll-Container und verbreitern mobil nicht die Seite. */}
      <div className="relative overflow-x-auto">
        <table className="tabelle min-w-[640px]">
          <thead>
            <tr>
              <th scope="col">#</th>
              <th scope="col">Text</th>
              <th scope="col" className="num">
                Menge
              </th>
              <th scope="col" className="num">
                Einzel
              </th>
              <th scope="col" className="num">
                Gesamt
              </th>
              <th scope="col">Artikel</th>
              <th scope="col" className="num">
                Grundpreis
              </th>
              {darfBearbeiten && (
                <th scope="col">
                  <span className="sr-only">Aktionen</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {zeilen.map((p, index) =>
              bearbeitet?.index === index ? (
                <EditZeile
                  key={index}
                  nr={index + 1}
                  entwurf={bearbeitet.entwurf}
                  fehler={bearbeitet.fehler}
                  onAendern={feldAendern}
                  onUebernehmen={uebernehmen}
                  onAbbrechen={() => setBearbeitet(null)}
                />
              ) : (
                <tr key={index}>
                  <td className="gedaempft">{index + 1}</td>
                  <td>{p.text}</td>
                  <td className="num">{mengeText(p)}</td>
                  <td className="num">
                    {p.einzelpreis === null || p.einzelpreis === undefined ? '—' : formatZahl(p.einzelpreis, 2)}
                  </td>
                  <td className="num font-medium">{formatZahl(p.gesamt, 2)}</td>
                  <td>
                    {p.stammartikelId ? (
                      <Link
                        to="/artikel/stammartikel/$stammartikelId"
                        params={{ stammartikelId: p.stammartikelId }}
                        className="text-xs"
                      >
                        {p.artikel?.name ?? 'Stammartikel'}
                      </Link>
                    ) : p.artikel ? (
                      <Link to="/artikel/$artikelId" params={{ artikelId: p.artikel.id }} className="text-xs">
                        {p.artikel.name}
                      </Link>
                    ) : (
                      <span className="gedaempft">—</span>
                    )}
                  </td>
                  <td className="num gedaempft">{grundpreisText(p)}</td>
                  {darfBearbeiten && (
                    <td className="whitespace-nowrap">
                      <button
                        type="button"
                        className="btn btn-klein w-[30px] border-transparent px-0"
                        aria-label={`Position ${index + 1} bearbeiten`}
                        onClick={() => setBearbeitet({ index, entwurf: entwurfAus(p) })}
                      >
                        <Icon name="edit" />
                      </button>
                      <button
                        type="button"
                        className="btn btn-klein w-[30px] border-transparent px-0"
                        aria-label={`Position ${index + 1} löschen`}
                        onClick={() => loeschen(index)}
                      >
                        <Icon name="delete" />
                      </button>
                    </td>
                  )}
                </tr>
              ),
            )}
            {neueZeile && (
              <EditZeile
                nr={zeilen.length + 1}
                entwurf={bearbeitet.entwurf}
                fehler={bearbeitet.fehler}
                onAendern={feldAendern}
                onUebernehmen={uebernehmen}
                onAbbrechen={() => setBearbeitet(null)}
              />
            )}
          </tbody>
        </table>
      </div>

      <div
        data-testid="summenvergleich"
        data-abweichung={abweichung}
        className={`flex flex-wrap items-center justify-between gap-2.5 rounded-[10px] px-3.5 py-3 text-[13px] ${
          abweichung ? 'bg-orange-flaeche text-orange-text' : 'bg-gruen-flaeche text-gruen-text'
        }`}
      >
        <span>
          Summe Positionen <strong>{formatEuro(summe)}</strong>
          {netto !== null && (
            <>
              {' '}
              · Netto laut Beleg <strong>{formatEuro(netto)}</strong> · Differenz{' '}
              <strong>{formatEuro(differenz ?? 0)}</strong> (Toleranz {formatEuro(toleranz)})
            </>
          )}
          <span className="sr-only">{abweichung ? ' – Abweichung über der Toleranz' : ' – stimmt'}</span>
        </span>
        {darfBearbeiten && (
          <button
            type="button"
            className="btn btn-klein"
            disabled={bearbeitet !== null}
            onClick={() => setBearbeitet({ index: zeilen.length, entwurf: leererEntwurf })}
          >
            + Position hinzufügen
          </button>
        )}
      </div>

      {darfBearbeiten && geaendert && (
        <div className="flex gap-2">
          <button
            type="button"
            className="btn btn-primaer"
            disabled={ersetzen.isPending || bearbeitet !== null}
            onClick={speichern}
          >
            Änderungen speichern
          </button>
          <button type="button" className="btn" onClick={verwerfen}>
            Verwerfen
          </button>
        </div>
      )}

      <p className="gedaempft m-0 text-xs">
        Positionstexte werden pro Korrespondent normalisiert und Artikeln zugeordnet. Grundpreis nur, wenn Menge und
        Einheit erkannt wurden.
      </p>
    </div>
  )
}

function EditZeile({
  nr,
  entwurf,
  fehler,
  onAendern,
  onUebernehmen,
  onAbbrechen,
}: {
  nr: number
  entwurf: Entwurf
  fehler?: string
  onAendern: (feld: keyof Entwurf, wert: string) => void
  onUebernehmen: () => void
  onAbbrechen: () => void
}) {
  const feld = (name: keyof Entwurf, label: string, klasse = '') => (
    <label className={`flex flex-col ${klasse}`}>
      <span className="label">{label}</span>
      <input className="eingabe" value={entwurf[name]} onChange={(e) => onAendern(name, e.target.value)} />
    </label>
  )
  return (
    <tr>
      <td className="gedaempft align-top">{nr}</td>
      <td colSpan={7}>
        <div className="flex flex-col gap-2">
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-6">
            {feld('text', 'Text', 'col-span-2 sm:col-span-6')}
            {feld('menge', 'Menge')}
            {feld('einheit', 'Einheit')}
            {feld('einzelpreis', 'Einzelpreis')}
            {feld('gesamt', 'Gesamt')}
            {feld('steuersatz', 'Steuersatz')}
          </div>
          {fehler && (
            <p role="alert" className="m-0 text-[13px] text-rot-text">
              {fehler}
            </p>
          )}
          <div className="flex gap-2">
            <button type="button" className="btn btn-klein btn-primaer" onClick={onUebernehmen}>
              Übernehmen
            </button>
            <button type="button" className="btn btn-klein" onClick={onAbbrechen}>
              Abbrechen
            </button>
          </div>
        </div>
      </td>
    </tr>
  )
}
