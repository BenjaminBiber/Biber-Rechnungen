import { Link } from '@tanstack/react-router'
import { useId, useMemo, useState, type ReactNode } from 'react'
import { useEinstellungen } from '@/api/einstellungen'
import { exportDateiHerunterladen, useExportErzeugen, useExportVorschau } from '@/api/export'
import { useKorrespondenten } from '@/api/korrespondenten'
import { useKategorien, useTags } from '@/api/tags'
import type { ExportAnfrage } from '@/api/typen'
import { useEntprellt } from '@/components/auswahl/gemeinsam'
import { Icon } from '@/components/Icon'
import { Seitenkopf } from '@/components/Seitenkopf'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { formatDatum, formatEuro } from '@/lib/format'
import { datumEinlesen } from '@/lib/zeitraum'
import { exportBezeichnung, schnellwahl } from './bezeichnung'
import { exportFehlertext } from './fehler'
import { LetzteExporte } from './LetzteExporte'
import { MehrfachAuswahl } from './MehrfachAuswahl'

export type ExportVorbelegung = {
  von: string
  bis: string
  kategorien: string[]
  korrespondenten: string[]
  tags: string[]
}

const ENTPRELLUNG_MS = 400
const MB = 1024 * 1024

/** Exportseite: Zeitraum, Inhalt und Filter wählen, Vorschau, Export erstellen, letzte Exporte. */
export function ExportSeite({ vorbelegung, heute }: { vorbelegung: ExportVorbelegung; heute: Date }) {
  const { darfBearbeiten } = useRolle()
  return (
    <main className="seite">
      <Seitenkopf titel="Export" />
      {darfBearbeiten && <ExportFormular vorbelegung={vorbelegung} heute={heute} />}
      <LetzteExporte />
    </main>
  )
}

function ExportFormular({ vorbelegung, heute }: { vorbelegung: ExportVorbelegung; heute: Date }) {
  const toast = useToast()
  const { data: einstellungen } = useEinstellungen()
  const { data: kategorien = [] } = useKategorien()
  const { data: korrespondenten = [] } = useKorrespondenten()
  const { data: tags = [] } = useTags()
  const erzeugen = useExportErzeugen()
  const vorgaben = useMemo(() => schnellwahl(heute), [heute])

  const [vonText, setVonText] = useState(formatDatum(vorbelegung.von))
  const [bisText, setBisText] = useState(formatDatum(vorbelegung.bis))
  const [belegliste, setBelegliste] = useState(true)
  const [format, setFormat] = useState('xlsx')
  const [summen, setSummen] = useState(true)
  const [vorsteuerGewaehlt, setVorsteuer] = useState(true)
  const [zip, setZip] = useState(false)
  const [kategorieIds, setKategorieIds] = useState(vorbelegung.kategorien)
  const [korrespondentIds, setKorrespondentIds] = useState(vorbelegung.korrespondenten)
  const [tagIds, setTagIds] = useState(vorbelegung.tags)
  const [ohneDubletten, setOhneDubletten] = useState(true)
  const [ohneZuPruefen, setOhneZuPruefen] = useState(false)

  const kleinunternehmer = einstellungen?.ust.status === 'Kleinunternehmer'
  const vorsteuer = vorsteuerGewaehlt && !kleinunternehmer
  const von = datumEinlesen(vonText)
  const bis = datumEinlesen(bisText)
  const reihenfolgeFalsch = von !== null && bis !== null && von > bis
  const inhaltGewaehlt = belegliste || summen || vorsteuer || zip

  const anfrage = useMemo<ExportAnfrage | undefined>(
    () =>
      von && bis && von <= bis
        ? {
            von,
            bis,
            bezeichnung: exportBezeichnung(von, bis, heute),
            belegliste,
            beleglisteFormat: format,
            summenJeKategorie: summen,
            vorsteuer,
            zipOriginale: zip,
            kategorieIds,
            korrespondentIds,
            tagIds,
            dublettenAusschliessen: ohneDubletten,
            zuPruefenAusschliessen: ohneZuPruefen,
          }
        : undefined,
    [
      von,
      bis,
      heute,
      belegliste,
      format,
      summen,
      vorsteuer,
      zip,
      kategorieIds,
      korrespondentIds,
      tagIds,
      ohneDubletten,
      ohneZuPruefen,
    ],
  )
  const entprellt = useEntprellt(anfrage, ENTPRELLUNG_MS)
  const { data: vorschau } = useExportVorschau(entprellt)

  function zeitraumWaehlen(neuVon: string, neuBis: string) {
    setVonText(formatDatum(neuVon))
    setBisText(formatDatum(neuBis))
  }

  async function erstellen() {
    if (!anfrage) return
    toast.info(`Export ${anfrage.bezeichnung} wird erstellt …`)
    try {
      const lauf = await erzeugen.mutateAsync(anfrage)
      await exportDateiHerunterladen(lauf.id)
    } catch (f) {
      toast.fehler(exportFehlertext(f, anfrage.bezeichnung), { erneut: () => void erstellen() })
    }
  }

  const zipText = vorschau?.zipGroesse ? ` · ca. ${Math.max(1, Math.round(vorschau.zipGroesse / MB))} MB` : ''
  const bezeichnung = anfrage?.bezeichnung ?? ''

  return (
    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_420px]">
      <div className="flex flex-col gap-6">
        <section className="karte flex flex-col gap-4 p-5" aria-labelledby="export-zeitraum">
          <h2 id="export-zeitraum" className="m-0 text-base font-semibold text-text">
            1. Zeitraum
          </h2>
          <div className="flex flex-wrap gap-2">
            {vorgaben.map((v) => {
              const aktiv = v.von === von && v.bis === bis
              return (
                <button
                  key={v.id}
                  type="button"
                  aria-pressed={aktiv}
                  onClick={() => zeitraumWaehlen(v.von, v.bis)}
                  className={`h-[34px] rounded-full border px-3.5 text-[13px] font-medium ${
                    aktiv
                      ? 'border-[#a9a8d6] bg-blau-flaeche text-blau-text'
                      : 'border-eingabe-rand bg-karte text-text hover:bg-hover'
                  }`}
                >
                  {v.text}
                </button>
              )
            })}
          </div>
          <div className="grid max-w-md grid-cols-2 gap-3">
            <Datumsfeld id="export-von" label="Von" wert={vonText} onAendern={setVonText} ungueltig={von === null} />
            <Datumsfeld
              id="export-bis"
              label="Bis"
              wert={bisText}
              onAendern={setBisText}
              ungueltig={bis === null || reihenfolgeFalsch}
            />
          </div>
          {(von === null || bis === null) && (
            <p className="m-0 text-sm text-rot-text">Datum im Format TT.MM.JJJJ eingeben.</p>
          )}
          {reihenfolgeFalsch && <p className="m-0 text-sm text-rot-text">„Bis“ liegt vor „Von“.</p>}
        </section>

        <section className="karte flex flex-col gap-3 p-5" aria-labelledby="export-inhalt">
          <h2 id="export-inhalt" className="m-0 text-base font-semibold text-text">
            2. Inhalt
          </h2>
          <InhaltOption
            titel="Belegliste"
            beschreibung="Eine Zeile je Beleg: Datum, Korrespondent, Nr., Kategorie, EÜR-Zeile, Netto/USt/Brutto je Steuersatz, Tags, Ablagepfad"
            gewaehlt={belegliste}
            onAendern={setBelegliste}
          >
            <select
              className="eingabe w-[170px] shrink-0"
              aria-label="Format der Belegliste"
              value={format}
              onChange={(e) => setFormat(e.target.value)}
            >
              <option value="xlsx">Excel (.xlsx)</option>
              <option value="csv">CSV (Semikolon)</option>
            </select>
          </InhaltOption>
          <InhaltOption
            titel="Summen je Kategorie"
            beschreibung="inkl. EÜR-Zeile, sofern an der Kategorie gepflegt"
            gewaehlt={summen}
            onAendern={setSummen}
          />
          {!kleinunternehmer && (
            <InhaltOption
              titel="Vorsteuer je UStVA-Zeitraum"
              beschreibung="quartalsweise laut Einstellung · ausgeblendet bei Kleinunternehmer"
              gewaehlt={vorsteuerGewaehlt}
              onAendern={setVorsteuer}
            />
          )}
          <InhaltOption
            titel="ZIP der Originale"
            beschreibung={`unveränderte Originaldateien (bei E-Rechnungen das XML) mit Prüfsummenliste${zipText}`}
            gewaehlt={zip}
            onAendern={setZip}
          />
        </section>

        <section className="karte flex flex-col gap-4 p-5" aria-labelledby="export-filter">
          <h2 id="export-filter" className="m-0 text-base font-semibold text-text">
            3. Filter
          </h2>
          <div className="grid gap-3 md:grid-cols-3">
            <MehrfachAuswahl
              label="Kategorien"
              leerText="Alle Kategorien"
              optionen={kategorien}
              gewaehlt={kategorieIds}
              onAendern={setKategorieIds}
            />
            <MehrfachAuswahl
              label="Korrespondenten"
              leerText="Alle"
              optionen={korrespondenten}
              gewaehlt={korrespondentIds}
              onAendern={setKorrespondentIds}
            />
            <MehrfachAuswahl label="Tags" leerText="Beliebig" optionen={tags} gewaehlt={tagIds} onAendern={setTagIds} />
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={ohneDubletten} onChange={(e) => setOhneDubletten(e.target.checked)} />
            Dubletten ausschließen
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={ohneZuPruefen} onChange={(e) => setOhneZuPruefen(e.target.checked)} />
            Belege mit „zu prüfen“ ausschließen
          </label>
        </section>
      </div>

      <aside className="karte flex flex-col gap-4 p-5 lg:sticky lg:top-6" aria-labelledby="export-vorschau">
        <h2 id="export-vorschau" className="m-0 text-base font-semibold text-text">
          Vorschau · {bezeichnung}
        </h2>
        {vorschau && (
          <>
            <dl className="m-0 grid grid-cols-2 gap-3">
              <Kennzahl titel="Belege" wert={String(vorschau.anzahlBelege)} />
              <Kennzahl titel="Summe brutto" wert={formatEuro(vorschau.summeBrutto)} />
              {vorschau.vorsteuer != null && <Kennzahl titel="Vorsteuer" wert={formatEuro(vorschau.vorsteuer)} />}
              <Kennzahl titel="Dubletten ausgeschlossen" wert={String(vorschau.dublettenAusgeschlossen)} />
            </dl>
            {vorschau.anzahlZuPruefen > 0 && anfrage && (
              <div role="alert" className="flex gap-2 rounded-lg bg-orange-flaeche p-3 text-sm text-orange-text">
                <Icon name="report" />
                <span>
                  <strong>{vorschau.anzahlZuPruefen} Belege im Zeitraum sind „zu prüfen“.</strong> Diese sind noch nicht
                  bestätigt.{' '}
                  <Link to="/belege" search={{ ansicht: 'ZuPruefen', von: anfrage.von, bis: anfrage.bis }}>
                    Jetzt prüfen
                  </Link>
                </span>
              </div>
            )}
            <table className="tabelle">
              <thead>
                <tr>
                  <th scope="col">Kategorie</th>
                  <th scope="col">EÜR-Zeile</th>
                  <th scope="col" className="text-right">
                    Brutto
                  </th>
                </tr>
              </thead>
              <tbody>
                {vorschau.kategorien.map((k) => (
                  <tr key={k.kategorie}>
                    <td>{k.kategorie}</td>
                    <td className="gedaempft">{k.euerZeile ?? '—'}</td>
                    <td className="text-right whitespace-nowrap tabular-nums">{formatEuro(k.brutto)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </>
        )}
        <button
          type="button"
          className="btn btn-primaer w-full"
          disabled={!inhaltGewaehlt || !anfrage || erzeugen.isPending}
          onClick={() => void erstellen()}
        >
          <Icon name="download" />
          Export erstellen
        </button>
        <span className="gedaempft text-center text-xs">Jeder Export wird im Änderungsprotokoll vermerkt.</span>
      </aside>
    </div>
  )
}

function Datumsfeld({
  id,
  label,
  wert,
  onAendern,
  ungueltig,
}: {
  id: string
  label: string
  wert: string
  onAendern: (wert: string) => void
  ungueltig: boolean
}) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className="eingabe"
        placeholder="TT.MM.JJJJ"
        value={wert}
        aria-invalid={ungueltig}
        onChange={(e) => onAendern(e.target.value)}
      />
    </div>
  )
}

function InhaltOption({
  titel,
  beschreibung,
  gewaehlt,
  onAendern,
  children,
}: {
  titel: string
  beschreibung: string
  gewaehlt: boolean
  onAendern: (wert: boolean) => void
  children?: ReactNode
}) {
  const id = useId()
  return (
    <div className="flex items-start gap-3 rounded-[10px] border border-rand p-3.5">
      <input
        id={id}
        type="checkbox"
        className="mt-1"
        checked={gewaehlt}
        onChange={(e) => onAendern(e.target.checked)}
        aria-describedby={`${id}-beschreibung`}
      />
      <span className="flex-1 text-sm">
        <label htmlFor={id} className="cursor-pointer font-semibold">
          {titel}
        </label>
        <br />
        <span id={`${id}-beschreibung`} className="gedaempft text-[13px]">
          {beschreibung}
        </span>
      </span>
      {children}
    </div>
  )
}

function Kennzahl({ titel, wert }: { titel: string; wert: string }) {
  return (
    <div className="rounded-lg bg-flaeche p-3">
      <dt className="gedaempft text-xs">{titel}</dt>
      <dd className="m-0 mt-1 text-lg font-semibold tabular-nums">{wert}</dd>
    </div>
  )
}
