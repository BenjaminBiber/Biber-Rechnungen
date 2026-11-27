import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { usePositionenErsetzen } from '@/api/belege'
import { useEinstellungen } from '@/api/einstellungen'
import type { BelegDetail, Position, PositionAendern } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { MerkmalChips } from '@/components/MerkmalChips'
import { useToast } from '@/components/toast'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatEuro, formatZahl } from '@/lib/format'
import { MERKMAL_NAMEN } from '@/lib/merkmale'
import { betragEingabe, parseBetrag, parseSatz, satzEingabe, STEUER_TOLERANZ } from '../gemeinsam'

type MerkmalEntwurf = { name: string; wert: string }

/** Eingabefelder einer Zeile im Bearbeiten-Modus (deutsche Schreibweise). */
type Entwurf = {
  text: string
  artikelnummer: string
  merkmale: MerkmalEntwurf[]
  menge: string
  einheit: string
  einzelpreis: string
  gesamt: string
  steuersatz: string
}

type Textfeld = Exclude<keyof Entwurf, 'merkmale'>

const leererEntwurf: Entwurf = {
  text: '',
  artikelnummer: '',
  merkmale: [],
  menge: '1',
  einheit: '',
  einzelpreis: '',
  gesamt: '',
  steuersatz: '19',
}

function entwurfAus(p: Position): Entwurf {
  return {
    text: p.text,
    artikelnummer: p.artikelnummer ?? '',
    merkmale: p.merkmale.map((m) => ({ name: m.name ?? '', wert: m.wert })),
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
    artikelnummer: p.artikelnummer ?? null,
    merkmale: p.merkmale,
    menge: p.menge ?? null,
    einheit: p.einheit ?? null,
    einzelpreis: p.einzelpreis ?? null,
    gesamt: p.gesamt,
    steuersatz: p.steuersatz ?? null,
  }
}

/** Artikelnummer und Merkmale unter dem Text der Position; nichts ohne beides. */
function Kennung({ p }: { p: Position }) {
  if (!p.artikelnummer && p.merkmale.length === 0) return null
  return (
    <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs">
      {p.artikelnummer && <span className="gedaempft">Art.-Nr. {p.artikelnummer}</span>}
      <MerkmalChips merkmale={p.merkmale} />
    </div>
  )
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

  function feldAendern(feld: Textfeld, wert: string) {
    if (!bearbeitet) return
    const entwurf = { ...bearbeitet.entwurf, [feld]: wert }
    if (feld === 'menge' || feld === 'einzelpreis') {
      const vorschlag = gesamtVorschlag(entwurf.menge, entwurf.einzelpreis)
      if (vorschlag !== null) entwurf.gesamt = vorschlag
    }
    setBearbeitet({ ...bearbeitet, entwurf, fehler: undefined })
  }

  function merkmaleAendern(merkmale: MerkmalEntwurf[]) {
    if (!bearbeitet) return
    setBearbeitet({ ...bearbeitet, entwurf: { ...bearbeitet.entwurf, merkmale }, fehler: undefined })
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
    const merkmale = entwurf.merkmale
      .map((m) => ({ name: m.name.trim() || null, wert: m.wert.trim() }))
      .filter((m) => m.name || m.wert)
    if (merkmale.some((m) => !m.wert))
      return setBearbeitet({ ...bearbeitet, fehler: 'Jedes Merkmal braucht einen Wert.' })
    const alt = zeilen[index] as Position | undefined
    const neu: Position = {
      ...(alt ?? { artikel: null, stammartikelId: null, grundpreis: null, grundpreisEinheit: null }),
      nr: alt?.nr ?? index + 1,
      text: entwurf.text.trim(),
      artikelnummer: entwurf.artikelnummer.trim() || null,
      merkmale,
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
            {zeilen.map((p, index) => (
              <tr key={index}>
                <td className="gedaempft">{index + 1}</td>
                <td>
                  {p.text}
                  <Kennung p={p} />
                </td>
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
            ))}
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
            aria-busy={ersetzen.isPending}
            onClick={speichern}
          >
            Änderungen speichern
          </button>
          <button type="button" className="btn" onClick={verwerfen}>
            Verwerfen
          </button>
        </div>
      )}

      {bearbeitet && (
        <PositionDialog
          titel={
            bearbeitet.index >= zeilen.length ? 'Position hinzufügen' : `Position ${bearbeitet.index + 1} bearbeiten`
          }
          entwurf={bearbeitet.entwurf}
          fehler={bearbeitet.fehler}
          merkmalNamen={merkmalNamen(zeilen)}
          onAendern={feldAendern}
          onMerkmaleAendern={merkmaleAendern}
          onUebernehmen={uebernehmen}
          onAbbrechen={() => setBearbeitet(null)}
        />
      )}

      <p className="gedaempft m-0 text-xs">
        Positionen werden pro Korrespondent über die Artikelnummer Artikeln zugeordnet, ohne Nummer über den
        normalisierten Text. Merkmale wie die Größe gehören zur Position, Positionen derselben Nummer zu einem Artikel.
        Grundpreis nur, wenn Menge und Einheit erkannt wurden.
      </p>
    </div>
  )
}

/** Vorschläge für Merkmalnamen: die üblichen und die schon im Beleg verwendeten. */
function merkmalNamen(zeilen: Position[]): string[] {
  return [...new Set([...MERKMAL_NAMEN, ...zeilen.flatMap((z) => z.merkmale.map((m) => m.name ?? ''))])].filter(Boolean)
}

/** Dialog zum Bearbeiten bzw. Hinzufügen einer Position; „Übernehmen“ ändert nur den lokalen Stand. */
function PositionDialog({
  titel,
  entwurf,
  fehler,
  merkmalNamen,
  onAendern,
  onMerkmaleAendern,
  onUebernehmen,
  onAbbrechen,
}: {
  titel: string
  entwurf: Entwurf
  fehler?: string
  merkmalNamen: string[]
  onAendern: (feld: Textfeld, wert: string) => void
  onMerkmaleAendern: (merkmale: MerkmalEntwurf[]) => void
  onUebernehmen: () => void
  onAbbrechen: () => void
}) {
  const feld = (name: Textfeld, label: string, klasse = '') => (
    <label className={`flex flex-col ${klasse}`}>
      <span className="label">{label}</span>
      <input className="eingabe" value={entwurf[name]} onChange={(e) => onAendern(name, e.target.value)} />
    </label>
  )
  return (
    <Dialog open onOpenChange={(offen) => !offen && onAbbrechen()}>
      <DialogContent className="max-w-[560px]" aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>{titel}</DialogTitle>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(e) => {
            e.preventDefault()
            onUebernehmen()
          }}
        >
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {feld('text', 'Text', 'col-span-2 sm:col-span-3')}
            {feld('artikelnummer', 'Artikelnummer')}
            {feld('menge', 'Menge')}
            {feld('einheit', 'Einheit')}
            {feld('steuersatz', 'Steuersatz')}
            {feld('einzelpreis', 'Einzelpreis')}
            {feld('gesamt', 'Gesamt')}
          </div>
          <MerkmalEditor merkmale={entwurf.merkmale} namen={merkmalNamen} onAendern={onMerkmaleAendern} />
          {fehler && (
            <p role="alert" className="m-0 text-[13px] text-rot-text">
              {fehler}
            </p>
          )}
          <DialogFooter>
            <button type="button" className="btn" onClick={onAbbrechen}>
              Abbrechen
            </button>
            <button type="submit" className="btn btn-primaer">
              Übernehmen
            </button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Merkmale als Zeilen aus Name (mit Vorschlägen) und Wert; der Name darf fehlen, der Wert nicht. */
function MerkmalEditor({
  merkmale,
  namen,
  onAendern,
}: {
  merkmale: MerkmalEntwurf[]
  namen: string[]
  onAendern: (merkmale: MerkmalEntwurf[]) => void
}) {
  const aendern = (index: number, teil: Partial<MerkmalEntwurf>) =>
    onAendern(merkmale.map((m, i) => (i === index ? { ...m, ...teil } : m)))
  return (
    <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
      <legend className="label mb-1.5 p-0">Merkmale</legend>
      {merkmale.length === 0 && <p className="gedaempft m-0 text-[13px]">Keine – z. B. Größe oder Farbe.</p>}
      {merkmale.map((m, i) => (
        <div key={i} className="flex items-center gap-2">
          <input
            className="eingabe w-2/5 min-w-0"
            aria-label={`Merkmal ${i + 1}: Name`}
            placeholder="Name, z. B. Größe"
            list="merkmal-namen"
            value={m.name}
            onChange={(e) => aendern(i, { name: e.target.value })}
          />
          <input
            className="eingabe min-w-0 flex-1"
            aria-label={`Merkmal ${i + 1}: Wert`}
            placeholder="Wert, z. B. 42"
            value={m.wert}
            onChange={(e) => aendern(i, { wert: e.target.value })}
          />
          <button
            type="button"
            className="btn btn-klein w-[30px] shrink-0 border-transparent px-0"
            aria-label={`Merkmal ${i + 1} entfernen`}
            onClick={() => onAendern(merkmale.filter((_, j) => j !== i))}
          >
            <Icon name="close" />
          </button>
        </div>
      ))}
      <button
        type="button"
        className="btn btn-klein self-start"
        onClick={() => onAendern([...merkmale, { name: '', wert: '' }])}
      >
        + Merkmal
      </button>
      <datalist id="merkmal-namen">
        {namen.map((n) => (
          <option key={n} value={n} />
        ))}
      </datalist>
    </fieldset>
  )
}
