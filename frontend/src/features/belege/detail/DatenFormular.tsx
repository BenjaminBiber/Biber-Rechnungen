import { useQueryClient } from '@tanstack/react-query'
import { useBlocker } from '@tanstack/react-router'
import { useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { belegeKeys, useBelegAendern, useTagEntfernen, useTagHinzufuegen } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import { useKorrespondenten } from '@/api/korrespondenten'
import { useKategorien } from '@/api/tags'
import type { BelegDetail, Belegart, KorrespondentErkennung } from '@/api/typen'
import { KategorieAuswahl } from '@/components/auswahl/KategorieAuswahl'
import { KorrespondentAuswahl } from '@/components/auswahl/KorrespondentAuswahl'
import { TagAuswahl } from '@/components/auswahl/TagAuswahl'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { formatDatumZeit, formatZahl } from '@/lib/format'
import { parseBetrag, parseSatz, seitenText, steuerStimmt } from './gemeinsam'
import { ausBeleg, gleich, pruefen, serverFehler, zuDto, type FeldFehler, type Formular } from './formular'

const belegarten: Belegart[] = ['Rechnung', 'Gutschrift', 'Quittung']

const erkennung: Record<KorrespondentErkennung, string | null> = {
  UstIdNr: 'erkannt über USt-IdNr.',
  Iban: 'erkannt über IBAN',
  Name: 'erkannt über Name',
  Manuell: 'manuell zugeordnet',
  Keine: null,
}

/** Feld mit Label, Hinweis und Fehlermeldung (per `aria-describedby` am Eingabefeld). */
function Feld({
  id,
  label,
  fehler,
  hinweis,
  children,
}: {
  id: string
  label: string
  fehler?: string
  hinweis?: ReactNode
  children: ReactNode
}) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      {children}
      {fehler && (
        <span id={`${id}-fehler`} role="alert" className="hinweis text-rot-text">
          {fehler}
        </span>
      )}
      {hinweis && !fehler && <span className="hinweis">{hinweis}</span>}
    </div>
  )
}

const auswahlKnopf =
  'eingabe flex cursor-pointer items-center gap-2 text-left disabled:cursor-default disabled:bg-flaeche disabled:text-gedaempft-stark'

/** Tab „Daten“: Kopfdaten, Tags, Beträge und Steuerzeilen eines Belegs korrigieren. */
export function DatenFormular({
  beleg,
  darfBearbeiten,
  fokus,
  onFokusErledigt,
}: {
  beleg: BelegDetail
  darfBearbeiten: boolean
  fokus?: 'belegart'
  onFokusErledigt?: () => void
}) {
  const id = useId()
  const toast = useToast()
  const queryClient = useQueryClient()
  const aendern = useBelegAendern()
  const tagHinzufuegen = useTagHinzufuegen()
  const tagEntfernen = useTagEntfernen()
  const { data: korrespondenten = [] } = useKorrespondenten()
  const { data: kategorien = [] } = useKategorien()
  const belegartRef = useRef<HTMLSelectElement>(null)

  // `entwurf` ist null, solange nichts geändert wurde – dann gelten die Daten des Belegs.
  const basis = ausBeleg(beleg)
  const [entwurf, setEntwurf] = useState<Formular | null>(null)
  const [fehler, setFehler] = useState<FeldFehler>({})
  const werte = entwurf ?? basis
  const geaendert = entwurf !== null && !gleich(entwurf, basis)

  const blocker = useBlocker({
    shouldBlockFn: ({ current, next }) => geaendert && current.pathname !== next.pathname,
    enableBeforeUnload: () => geaendert,
    withResolver: true,
  })

  useEffect(() => {
    if (fokus !== 'belegart') return
    belegartRef.current?.focus()
    onFokusErledigt?.()
  }, [fokus, onFokusErledigt])

  function setze<K extends keyof Formular>(feld: K, wert: Formular[K]) {
    setEntwurf({ ...werte, [feld]: wert })
    if (fehler[feld]) setFehler({ ...fehler, [feld]: undefined })
  }

  function setzeZeile(index: number, feld: 'satz' | 'netto' | 'steuer', wert: string) {
    setze(
      'steuerzeilen',
      werte.steuerzeilen.map((z, i) => (i === index ? { ...z, [feld]: wert } : z)),
    )
    const schluessel = `steuerzeilen.${index}.${feld}`
    if (fehler[schluessel]) setFehler({ ...fehler, [schluessel]: undefined })
  }

  function verwerfen() {
    setEntwurf(null)
    setFehler({})
  }

  async function speichern() {
    const pruefung = pruefen(werte)
    if (Object.keys(pruefung).length > 0) {
      setFehler(pruefung)
      return
    }
    try {
      const neu = await aendern.mutateAsync({ id: beleg.id, ...zuDto(werte, beleg.titel) })
      queryClient.setQueryData(belegeKeys.detail(beleg.id), neu)
      setEntwurf(null)
      setFehler({})
      toast.erfolg('Gespeichert')
    } catch (e) {
      if (istApiFehler(e) && e.fehler) setFehler(serverFehler(e.fehler))
      else toast.fehler('Speichern fehlgeschlagen')
    }
  }

  async function tagsAendern(ids: string[]) {
    const vorher = beleg.tags.map((t) => t.id)
    try {
      for (const tagId of ids.filter((t) => !vorher.includes(t)))
        await tagHinzufuegen.mutateAsync({ id: beleg.id, tagId })
      for (const tag of beleg.tags.filter((t) => !t.istSystem && !ids.includes(t.id)))
        await tagEntfernen.mutateAsync({ id: beleg.id, tagId: tag.id })
    } catch {
      toast.fehler('Tags konnten nicht geändert werden')
    }
  }

  async function tagLoesen(tagId: string) {
    try {
      await tagEntfernen.mutateAsync({ id: beleg.id, tagId })
    } catch {
      toast.fehler('Tag konnte nicht entfernt werden')
    }
  }

  const nurLesen = !darfBearbeiten
  const korrespondent = korrespondenten.find((k) => k.id === werte.korrespondentId)
  const korrespondentName =
    korrespondent?.name ?? (werte.korrespondentId === beleg.korrespondent?.id ? beleg.korrespondent?.name : undefined)
  const korrespondentInitialen =
    korrespondent?.logo.initialen ?? (werte.korrespondentId === beleg.korrespondent?.id ? beleg.logo.initialen : '')
  const korrespondentHinweis =
    werte.korrespondentId !== (beleg.korrespondent?.id ?? null)
      ? 'manuell zugeordnet'
      : erkennung[beleg.korrespondentErkennung]
  const kategorieName =
    kategorien.find((k) => k.id === werte.kategorieId)?.name ??
    (werte.kategorieId === beleg.kategorie?.id ? beleg.kategorie?.name : undefined)
  const defaultName = beleg.defaultKategorie?.name
  const kategorieHinweis = !werte.kategorieId
    ? defaultName && `leer · Default: ${defaultName}`
    : beleg.kategorieManuell || werte.kategorieId !== (beleg.kategorie?.id ?? null)
      ? `manuell geändert${defaultName ? ` · Default: ${defaultName}` : ''}`
      : undefined
  const waehrungen = Array.from(new Set(['EUR', werte.waehrung]))

  const eingabe = (feld: 'rechnungsnummer' | 'rechnungsdatum' | 'leistung' | 'faelligkeitsdatum', extra = '') => ({
    id: `${id}-${feld}`,
    className: `eingabe ${extra}`,
    value: werte[feld],
    readOnly: nurLesen,
    'aria-invalid': fehler[feld] ? true : undefined,
    'aria-describedby': fehler[feld] ? `${id}-${feld}-fehler` : undefined,
    onChange: (e: { target: { value: string } }) => setze(feld, e.target.value),
  })

  const betragFeld = (feld: 'netto' | 'steuer' | 'brutto', label: string) => (
    <Feld id={`${id}-${feld}`} label={label} fehler={fehler[feld]}>
      <input
        id={`${id}-${feld}`}
        className={`eingabe num ${feld === 'brutto' ? 'font-semibold' : ''}`}
        inputMode="decimal"
        value={werte[feld]}
        readOnly={nurLesen}
        aria-invalid={fehler[feld] ? true : undefined}
        aria-describedby={fehler[feld] ? `${id}-${feld}-fehler` : undefined}
        onChange={(e) => setze(feld, e.target.value)}
      />
    </Feld>
  )

  const extraktion = beleg.extraktion
  const konfidenz = extraktion.konfidenz ?? null

  return (
    <form
      className="flex flex-col gap-[18px]"
      aria-label="Daten"
      onSubmit={(e) => {
        e.preventDefault()
        if (geaendert) void speichern()
      }}
    >
      <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-3.5">
        <Feld
          id={`${id}-korrespondent`}
          label="Korrespondent"
          hinweis={korrespondentHinweis}
          fehler={fehler.korrespondentId}
        >
          <KorrespondentAuswahl
            wert={werte.korrespondentId}
            onAendern={(k) => setze('korrespondentId', k)}
            ausloeser={
              <button type="button" id={`${id}-korrespondent`} className={auswahlKnopf} disabled={nurLesen}>
                {korrespondentInitialen && (
                  <span
                    aria-hidden
                    className="flex size-[22px] flex-none items-center justify-center rounded-[5px] bg-orange-flaeche text-[10px] font-bold text-orange-text"
                  >
                    {korrespondentInitialen}
                  </span>
                )}
                <span className="min-w-0 flex-1 truncate">{korrespondentName ?? 'Kein Korrespondent'}</span>
                {!nurLesen && <Icon name="expand_more" className="text-gedaempft" />}
              </button>
            }
          />
        </Feld>
        <Feld id={`${id}-belegart`} label="Belegart">
          <select
            id={`${id}-belegart`}
            ref={belegartRef}
            className="eingabe"
            value={werte.belegart}
            disabled={nurLesen}
            onChange={(e) => setze('belegart', e.target.value as Belegart)}
          >
            {belegarten.map((art) => (
              <option key={art}>{art}</option>
            ))}
          </select>
        </Feld>
        <Feld id={`${id}-rechnungsnummer`} label="Rechnungsnummer" fehler={fehler.rechnungsnummer}>
          <input {...eingabe('rechnungsnummer', 'mono')} />
        </Feld>
        <Feld id={`${id}-rechnungsdatum`} label="Rechnungsdatum" fehler={fehler.rechnungsdatum}>
          <input {...eingabe('rechnungsdatum')} placeholder="TT.MM.JJJJ" />
        </Feld>
        <Feld id={`${id}-leistung`} label="Leistungsdatum / -zeitraum" fehler={fehler.leistung}>
          <input {...eingabe('leistung')} placeholder="TT.MM.JJJJ oder von – bis" />
        </Feld>
        <Feld id={`${id}-faelligkeitsdatum`} label="Fällig am" fehler={fehler.faelligkeitsdatum}>
          <input {...eingabe('faelligkeitsdatum')} placeholder="TT.MM.JJJJ" />
        </Feld>
        <Feld id={`${id}-kategorie`} label="Kategorie" hinweis={kategorieHinweis} fehler={fehler.kategorieId}>
          <KategorieAuswahl
            wert={werte.kategorieId}
            onAendern={(k) => setze('kategorieId', k)}
            leerText={defaultName ? `Leer lassen = Default: ${defaultName}` : undefined}
            ausloeser={
              <button type="button" id={`${id}-kategorie`} className={auswahlKnopf} disabled={nurLesen}>
                <span className="min-w-0 flex-1 truncate">{kategorieName ?? 'Keine Kategorie'}</span>
                {!nurLesen && <Icon name="expand_more" className="text-gedaempft" />}
              </button>
            }
          />
        </Feld>
        <Feld id={`${id}-waehrung`} label="Währung" fehler={fehler.waehrung}>
          <select
            id={`${id}-waehrung`}
            className="eingabe"
            value={werte.waehrung}
            disabled={nurLesen}
            onChange={(e) => setze('waehrung', e.target.value)}
          >
            {waehrungen.map((w) => (
              <option key={w}>{w}</option>
            ))}
          </select>
        </Feld>
      </div>

      <div>
        <span className="label">Tags</span>
        <div className="flex min-h-9 flex-wrap items-center gap-1.5 rounded-[8px] border border-eingabe-rand px-2 py-1.5">
          {beleg.tags.map((tag) =>
            tag.istSystem ? (
              <span key={tag.id} title="System-Tag, wird nur durch Bestätigen entfernt">
                <Chip ton="orange" icon="lock">
                  {tag.name}
                </Chip>
              </span>
            ) : (
              <Chip
                key={tag.id}
                ton={tag.farbe as ChipTon}
                onEntfernen={nurLesen ? undefined : () => void tagLoesen(tag.id)}
              >
                {tag.name}
              </Chip>
            ),
          )}
          {!nurLesen && (
            <TagAuswahl
              gewaehlt={beleg.tags.map((t) => t.id)}
              onAendern={(ids) => void tagsAendern(ids)}
              ausloeser={
                <button
                  type="button"
                  className="cursor-pointer border-0 bg-transparent text-[13px] font-medium text-akzent-text"
                >
                  + Tag hinzufügen
                </button>
              }
            />
          )}
          {nurLesen && beleg.tags.length === 0 && <span className="gedaempft text-[13px]">Keine Tags</span>}
        </div>
      </div>

      <div>
        <h2 className="m-0 mb-2.5 text-[15px] font-semibold">Beträge</h2>
        <div className="grid grid-cols-3 gap-3">
          {betragFeld('netto', 'Netto')}
          {betragFeld('steuer', 'USt')}
          {betragFeld('brutto', 'Brutto')}
        </div>
        <div className="mt-3 overflow-x-auto">
          <table className="tabelle text-[13px]" aria-label="Steuerzeilen">
            <thead>
              <tr>
                <th scope="col">Steuersatz</th>
                <th scope="col" className="num">
                  Netto
                </th>
                <th scope="col" className="num">
                  Steuer
                </th>
                <th scope="col" className="num">
                  Prüfung
                </th>
                {!nurLesen && (
                  <th scope="col">
                    <span className="sr-only">Aktionen</span>
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {werte.steuerzeilen.map((zeile, i) => {
                const satz = parseSatz(zeile.satz)
                const netto = parseBetrag(zeile.netto)
                const steuer = parseBetrag(zeile.steuer)
                const pruefbar = satz !== null && netto !== null && steuer !== null
                const nr = i + 1
                const zelle = (feld: 'satz' | 'netto' | 'steuer', label: string) => {
                  const schluessel = `steuerzeilen.${i}.${feld}`
                  return nurLesen ? (
                    feld === 'satz' ? (
                      `${zeile.satz} %`
                    ) : (
                      `${zeile[feld]} €`
                    )
                  ) : (
                    <>
                      <input
                        className={`eingabe h-8 ${feld === 'satz' ? 'w-20' : 'num w-28'}`}
                        aria-label={`${label} Steuerzeile ${nr}`}
                        inputMode="decimal"
                        value={zeile[feld]}
                        aria-invalid={fehler[schluessel] ? true : undefined}
                        onChange={(e) => setzeZeile(i, feld, e.target.value)}
                      />
                      {fehler[schluessel] && (
                        <span role="alert" className="hinweis text-rot-text">
                          {fehler[schluessel]}
                        </span>
                      )}
                    </>
                  )
                }
                return (
                  <tr key={i}>
                    <td>{zelle('satz', 'Steuersatz')}</td>
                    <td className="num">{zelle('netto', 'Netto')}</td>
                    <td className="num">{zelle('steuer', 'Steuer')}</td>
                    <td className="num">
                      {!pruefbar ? (
                        <span className="gedaempft">—</span>
                      ) : steuerStimmt(satz, netto, steuer) ? (
                        <Chip ton="gruen">stimmt</Chip>
                      ) : (
                        <Chip ton="orange">weicht ab</Chip>
                      )}
                    </td>
                    {!nurLesen && (
                      <td className="w-10">
                        <button
                          type="button"
                          className="inline-flex size-8 cursor-pointer items-center justify-center rounded-md border-0 bg-transparent text-gedaempft-stark hover:bg-hover"
                          aria-label={`Steuerzeile ${nr} entfernen`}
                          onClick={() =>
                            setze(
                              'steuerzeilen',
                              werte.steuerzeilen.filter((_, j) => j !== i),
                            )
                          }
                        >
                          <Icon name="delete" />
                        </button>
                      </td>
                    )}
                  </tr>
                )
              })}
              {werte.steuerzeilen.length === 0 && (
                <tr>
                  <td colSpan={nurLesen ? 4 : 5} className="gedaempft">
                    Keine Steuerzeilen
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        {!nurLesen && (
          <button
            type="button"
            className="mt-2 cursor-pointer border-0 bg-transparent p-0 text-[13px] font-medium text-akzent-text"
            onClick={() => setze('steuerzeilen', [...werte.steuerzeilen, { satz: '19', netto: '', steuer: '' }])}
          >
            + Steuerzeile
          </button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-3 rounded-[10px] bg-flaeche px-3.5 py-3 text-[13px] text-gedaempft-stark">
        <Icon name={extraktion.quelle === 'Manuell' ? 'edit_note' : 'document_scanner'} />
        <span className="flex-[1_1_200px]">
          {extraktion.quelle === 'DocumentIntelligence' ? (
            <>
              Ausgelesen mit <strong className="text-text">Document Intelligence</strong> (prebuilt-invoice)
              {extraktion.zeitpunkt && ` · ${formatDatumZeit(extraktion.zeitpunkt).replace(',', '')}`} ·{' '}
              {seitenText(extraktion.seiten)}
            </>
          ) : extraktion.quelle === 'Manuell' ? (
            'Manuell erfasst'
          ) : (
            `Aus E-Rechnung (${extraktion.quelle}) übernommen`
          )}
        </span>
        {extraktion.quelle === 'DocumentIntelligence' && konfidenz !== null && (
          <span className="flex items-center gap-1.5">
            Konfidenz
            <span
              role="meter"
              aria-label="Konfidenz"
              aria-valuemin={0}
              aria-valuemax={1}
              aria-valuenow={konfidenz}
              className="inline-block h-1.5 w-[70px] rounded-[3px] bg-rand"
            >
              <span
                className={`block h-1.5 rounded-[3px] ${konfidenz < 0.8 ? 'bg-orange-text' : 'bg-gruen-text'}`}
                style={{ width: `${Math.round(konfidenz * 100)}%` }}
              />
            </span>
            <strong className={konfidenz < 0.8 ? 'text-orange-text' : 'text-gruen-text'}>
              {formatZahl(konfidenz, 2)}
            </strong>
          </span>
        )}
      </div>

      {!nurLesen && (
        <div className="flex justify-end gap-2">
          <button type="button" className="btn" onClick={verwerfen} disabled={!geaendert}>
            Verwerfen
          </button>
          <button type="submit" className="btn btn-primaer" disabled={!geaendert || aendern.isPending}>
            Änderungen speichern
          </button>
        </div>
      )}

      <AlertDialog open={blocker.status === 'blocked'} onOpenChange={(offen) => !offen && blocker.reset?.()}>
        <AlertDialogContent>
          <AlertDialogTitle>Ungespeicherte Änderungen verwerfen?</AlertDialogTitle>
          <AlertDialogDescription>
            Die Änderungen an Beleg {beleg.rechnungsnummer ?? beleg.titel} sind noch nicht gespeichert.
          </AlertDialogDescription>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => blocker.reset?.()}>Weiter bearbeiten</AlertDialogCancel>
            <button type="button" className="btn btn-gefaehrlich" onClick={() => blocker.proceed?.()}>
              Verwerfen und verlassen
            </button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </form>
  )
}
