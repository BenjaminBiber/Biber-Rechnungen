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
import { Datumseingabe } from '@/components/Datumseingabe'
import { Icon } from '@/components/Icon'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { formatDatumZeit, formatZahl } from '@/lib/format'
import { ibanFormatieren } from '@/lib/iban'
import { cn } from '@/lib/utils'
import { BetraegeBereich } from './BetraegeBereich'
import { istZuPruefen, seitenText } from './gemeinsam'
import { ausBeleg, gleich, MAX_TITEL, pruefen, serverFehler, zuDto, type FeldFehler, type Formular } from './formular'
import { aenderungenAnmelden } from './offeneAenderungen'
import { ZahlungBereich } from './ZahlungBereich'

const belegarten: Belegart[] = ['Rechnung', 'Gutschrift', 'Quittung']

/** Stand des Auto-Saves: Icon unten rechts in der Karte, Text beim Hovern. */
type Status = 'bereit' | 'offen' | 'speichert' | 'gespeichert' | 'ungueltig' | 'fehlgeschlagen'

const statusAnzeige: Record<Status, { icon: string; text: string; klasse?: string }> = {
  bereit: { icon: 'cloud_done', text: 'Änderungen werden automatisch gespeichert' },
  offen: { icon: 'cloud', text: 'Ungespeicherte Änderungen' },
  speichert: { icon: 'progress_activity', text: 'Speichert …', klasse: '[&_.material-symbols-outlined]:animate-spin' },
  gespeichert: {
    icon: 'cloud_done',
    text: 'Gespeichert',
    klasse: 'text-gruen-text icon-gefuellt',
  },
  ungueltig: { icon: 'cloud_off', text: 'Nicht gespeichert – Eingaben prüfen', klasse: 'text-rot-text' },
  fehlgeschlagen: { icon: 'cloud_off', text: 'Speichern fehlgeschlagen', klasse: 'text-rot-text' },
}

const erkennung: Record<KorrespondentErkennung, string | null> = {
  UstIdNr: 'erkannt über USt-IdNr.',
  Iban: 'erkannt über IBAN',
  Name: 'erkannt über Name',
  Manuell: 'manuell zugeordnet',
  Auto: 'automatisch zugeordnet',
  Keine: null,
}

/** Felder mit Anzeige des vorherigen Werts (die Beträge haben eigene Prüfhinweise). */
type VorherFeld = Exclude<keyof Formular, 'netto' | 'steuer' | 'brutto' | 'steuerzeilen'>

/** Wert eines Felds vor der ersten Änderung in dieser Ansicht – zum Anzeigen und Zurücksetzen. */
type Vorher = { feld: VorherFeld; text: string; mono?: boolean }

/** Rückfrage vor dem Zurücksetzen eines Felds. */
type ZuruecksetzenFrage = Vorher & { label: string; fokusId: string }

/**
 * Feld mit Label, Hinweis und Fehlermeldung (per `aria-describedby` am Eingabefeld). Ist es geändert (`vorher`),
 * sitzt rechts im Eingabefeld ein Knopf zum Zurücksetzen – bei `rechtsBelegt` (Kalender, Auswahlpfeil) links davon.
 */
function Feld({
  id,
  label,
  fehler,
  hinweis,
  vorher,
  rechtsBelegt = false,
  onZuruecksetzen,
  children,
}: {
  id: string
  label: string
  fehler?: string
  hinweis?: ReactNode
  vorher?: Vorher
  rechtsBelegt?: boolean
  onZuruecksetzen?: (frage: ZuruecksetzenFrage) => void
  children: ReactNode
}) {
  const knopf = vorher && onZuruecksetzen
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      <div className={cn('relative', knopf && (rechtsBelegt ? '[&_.eingabe]:pr-16' : '[&_.eingabe]:pr-9'))}>
        {children}
        {knopf && (
          <button
            type="button"
            aria-label={`${label} zurücksetzen`}
            title={`Geändert – zurücksetzen auf ${vorher.text ? `„${vorher.text}“` : 'leer'}`}
            className={cn(
              'absolute top-1/2 grid size-7 -translate-y-1/2 cursor-pointer place-items-center rounded-md border-0 bg-transparent text-akzent-text hover:bg-hover focus-visible:outline-2 focus-visible:outline-akzent',
              rechtsBelegt ? 'right-8' : 'right-1',
            )}
            onClick={() => onZuruecksetzen({ ...vorher, label, fokusId: id })}
          >
            <Icon name="history" className="text-[18px]" />
          </button>
        )}
      </div>
      {fehler && (
        <span id={`${id}-fehler`} role="alert" className="hinweis text-rot-text">
          {fehler}
        </span>
      )}
      {hinweis && !fehler && <span className="hinweis">{hinweis}</span>}
    </div>
  )
}

// Unsichtbar bis Hover/Fokus, aber per Tastatur erreichbar (sr-only statt hidden).
const statusKnopf =
  'sr-only cursor-pointer border-0 bg-transparent p-0 text-xs font-medium text-akzent-text group-hover:not-sr-only group-focus-within:not-sr-only'

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
  const [status, setStatus] = useState<Status>('bereit')
  // Stand vor dem letzten Speichern – „Rückgängig“ speichert ihn wieder.
  const [rueckgaengig, setRueckgaengig] = useState<Formular | null>(null)
  // Wert jedes Felds vor seiner ersten Änderung in dieser Ansicht; bleibt nach dem Auto-Save erhalten.
  const [ursprung, setUrsprung] = useState<{ belegId: string; werte: Partial<Formular> }>({
    belegId: beleg.id,
    werte: {},
  })
  const ursprungWerte = ursprung.belegId === beleg.id ? ursprung.werte : {}
  // Die Frage bleibt nach dem Schließen stehen: für die Schließ-Animation und den Fokus zurück ins Feld.
  const [frage, setFrage] = useState<ZuruecksetzenFrage | null>(null)
  const [frageOffen, setFrageOffen] = useState(false)
  const werte = entwurf ?? basis

  // Speichern läuft asynchron nach Blur/Auswahl: Refs liefern den aktuellen Stand ohne auf ein Re-Render zu warten.
  const entwurfRef = useRef<Formular | null>(null)
  // Stand des Servers aus dem Cache – nach dem Speichern sofort aktuell, auch vor dem nächsten Render.
  const basisAktuell = () => ausBeleg(queryClient.getQueryData<BelegDetail>(belegeKeys.detail(beleg.id)) ?? beleg)
  // Felder (per `name`), die schon verlassen wurden: nur deren Prüffehler zeigt das Auto-Save an.
  const beruehrt = useRef(new Set<string>())
  // Speichervorgänge laufen nacheinander, damit keine ältere Antwort einen neueren Stand überschreibt.
  const kette = useRef<Promise<unknown>>(Promise.resolve())

  const offen = () => entwurfRef.current !== null && !gleich(entwurfRef.current, basisAktuell())

  function entwurfSetzen(neu: Formular | null) {
    entwurfRef.current = neu
    setEntwurf(neu)
  }

  /** Speichert `stand` (den Entwurf beim Auslösen). Anzeige nur anpassen, solange er noch der aktuelle Entwurf ist. */
  async function speichernJetzt(stand: Formular | null, alleFehler: boolean): Promise<boolean> {
    const aktuell = entwurfRef.current === stand
    if (!stand || gleich(stand, basisAktuell())) {
      if (aktuell) {
        if (stand) entwurfSetzen(null)
        setFehler({})
        setStatus((s) => (s === 'gespeichert' ? s : 'bereit'))
      }
      return true
    }
    const pruefung = pruefen(stand)
    if (Object.keys(pruefung).length > 0) {
      if (!aktuell) return false
      const sichtbar = alleFehler
        ? pruefung
        : Object.fromEntries(Object.entries(pruefung).filter(([k]) => beruehrt.current.has(k)))
      setFehler(sichtbar)
      setStatus(Object.keys(sichtbar).length > 0 ? 'ungueltig' : 'offen')
      return false
    }
    const vorher = basisAktuell()
    setStatus('speichert')
    try {
      const neu = await aendern.mutateAsync({ id: beleg.id, ...zuDto(stand) })
      queryClient.setQueryData(belegeKeys.detail(beleg.id), neu)
      // Während des Speicherns weitergetippt? Dann bleibt der neuere Entwurf stehen und wird beim nächsten Blur gespeichert.
      if (entwurfRef.current === stand) entwurfSetzen(null)
      setFehler({})
      beruehrt.current.clear()
      setRueckgaengig(vorher)
      setStatus(entwurfRef.current === null ? 'gespeichert' : 'offen')
      return true
    } catch (e) {
      if (istApiFehler(e) && e.fehler) {
        setFehler(serverFehler(e.fehler))
        setStatus('ungueltig')
      } else {
        setStatus('fehlgeschlagen')
        toast.fehler('Speichern fehlgeschlagen')
      }
      return false
    }
  }

  /**
   * Speichert den jetzigen Entwurf, falls er geändert und gültig ist – nach einem eventuell noch laufenden Speichern.
   * `alleFehler` zeigt auch Fehler unberührter Felder.
   */
  function speichern(alleFehler = false): Promise<boolean> {
    const stand = entwurfRef.current
    const lauf = kette.current.then(() => speichernJetzt(stand, alleFehler))
    kette.current = lauf.catch(() => undefined)
    return lauf
  }
  const speichernRef = useRef(speichern)
  useEffect(() => {
    speichernRef.current = speichern
  })

  useEffect(() => aenderungenAnmelden(beleg.id, () => speichernRef.current(true)), [beleg.id])

  // Beim Verlassen wird gespeichert; nachgefragt wird nur, wenn das nicht geht (ungültig oder Fehler).
  const blocker = useBlocker({
    shouldBlockFn: async ({ current, next }) =>
      current.pathname !== next.pathname && !(await speichernRef.current(true)),
    enableBeforeUnload: offen,
    withResolver: true,
  })

  useEffect(() => {
    if (fokus !== 'belegart') return
    belegartRef.current?.focus()
    onFokusErledigt?.()
  }, [fokus, onFokusErledigt])

  /** Übernimmt eine Eingabe; `sofort` (Auswahlfelder, Zeile entfernen) speichert gleich, Textfelder beim Verlassen. */
  function setze<K extends keyof Formular>(feld: K, wert: Formular[K], sofort = false) {
    const vorher = entwurfRef.current ?? basisAktuell()
    setUrsprung((u) => {
      const bisher = u.belegId === beleg.id ? u.werte : {}
      return feld in bisher ? u : { belegId: beleg.id, werte: { ...bisher, [feld]: vorher[feld] } }
    })
    entwurfSetzen({ ...vorher, [feld]: wert })
    setFehler((f) => (f[feld] ? { ...f, [feld]: undefined } : f))
    setStatus('offen')
    if (sofort) void speichern()
  }

  function setzeZeile(index: number, feld: 'satz' | 'netto' | 'steuer', wert: string) {
    const zeilen = (entwurfRef.current ?? basisAktuell()).steuerzeilen
    setze(
      'steuerzeilen',
      zeilen.map((z, i) => (i === index ? { ...z, [feld]: wert } : z)),
    )
    const schluessel = `steuerzeilen.${index}.${feld}`
    setFehler((f) => (f[schluessel] ? { ...f, [schluessel]: undefined } : f))
  }

  async function rueckgaengigMachen() {
    if (!rueckgaengig) return
    entwurfSetzen(rueckgaengig)
    if (await speichern(true)) setRueckgaengig(null)
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
  const korrespondentLogo =
    korrespondent?.logo ?? (werte.korrespondentId === beleg.korrespondent?.id ? beleg.logo : undefined)
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
  const bics = beleg.bankverbindungen.flatMap((b) => (b.bic ? [b.bic] : []))
  const bankBefund = beleg.befunde.find((b) => b.pruefung === 'Bankverbindung')?.ergebnis
  const ibanAbgleich =
    bankBefund === 'Fehlgeschlagen' ? (
      <span className="text-orange-text">weicht vom Korrespondenten ab</span>
    ) : bankBefund === 'Ok' ? (
      'beim Korrespondenten hinterlegt'
    ) : null
  const ibanHinweis =
    bics.length > 0 || ibanAbgleich ? (
      <>
        {bics.length > 0 && `BIC ${bics.join(', ')}`}
        {bics.length > 0 && ibanAbgleich && ' · '}
        {ibanAbgleich}
      </>
    ) : undefined

  const eingabe = (
    feld: 'titel' | 'rechnungsnummer' | 'rechnungsdatum' | 'leistung' | 'faelligkeitsdatum',
    extra = '',
  ) => ({
    id: `${id}-${feld}`,
    name: feld,
    className: `eingabe ${extra}`,
    value: werte[feld],
    readOnly: nurLesen,
    'aria-invalid': fehler[feld] ? true : undefined,
    'aria-describedby': fehler[feld] ? `${id}-${feld}-fehler` : undefined,
  })

  /** Vorheriger Wert eines geänderten Felds samt Zurücksetzen; `undefined`, solange das Feld unverändert ist. */
  function vorherVon<K extends VorherFeld>(
    feld: K,
    { anzeige, mono }: { anzeige?: (wert: Formular[K]) => string; mono?: boolean } = {},
  ): Vorher | undefined {
    if (nurLesen || !(feld in ursprungWerte)) return undefined
    const alt = ursprungWerte[feld] as Formular[K]
    if (alt === werte[feld]) return undefined
    return { feld, text: anzeige ? anzeige(alt) : String(alt ?? ''), mono }
  }
  function frageStellen(neu: ZuruecksetzenFrage) {
    setFrage(neu)
    setFrageOffen(true)
  }
  function zuruecksetzen(feld: VorherFeld) {
    if (!(feld in ursprungWerte)) return
    setze(feld, ursprungWerte[feld] as Formular[typeof feld], true)
  }
  const korrespondentNameVon = (kid: string | null) =>
    kid === null
      ? ''
      : (korrespondenten.find((k) => k.id === kid)?.name ??
        (kid === beleg.korrespondent?.id ? beleg.korrespondent.name : 'Unbekannter Korrespondent'))
  const kategorieNameVon = (kid: string | null) =>
    kid === null
      ? ''
      : (kategorien.find((k) => k.id === kid)?.name ??
        (kid === beleg.kategorie?.id ? beleg.kategorie.name : 'Unbekannte Kategorie'))

  const extraktion = beleg.extraktion
  const konfidenz = extraktion.konfidenz ?? null

  return (
    <form
      className="flex flex-col gap-[18px]"
      aria-label="Daten"
      onSubmit={(e) => {
        e.preventDefault()
        void speichern(true)
      }}
      onBlur={(e) => {
        // Textfelder speichern beim Verlassen; Auswahlfelder speichern schon bei der Änderung (`setze(…, true)`).
        const name = e.target instanceof HTMLInputElement ? e.target.name : ''
        if (nurLesen || !name) return
        beruehrt.current.add(name)
        void speichern()
      }}
    >
      <Feld
        id={`${id}-titel`}
        label="Titel"
        hinweis={nurLesen ? undefined : 'Wird in der Übersicht angezeigt. Leer = Rechnungsnummer bzw. Korrespondent.'}
        fehler={fehler.titel}
        onZuruecksetzen={frageStellen}
        vorher={vorherVon('titel')}
      >
        <input onChange={(e) => setze('titel', e.target.value)} {...eingabe('titel')} maxLength={MAX_TITEL} />
      </Feld>
      <div className="grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-3.5">
        <Feld
          id={`${id}-korrespondent`}
          label="Korrespondent"
          hinweis={korrespondentHinweis}
          fehler={fehler.korrespondentId}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('korrespondentId', { anzeige: korrespondentNameVon })}
        >
          <KorrespondentAuswahl
            wert={werte.korrespondentId}
            onAendern={(k) => setze('korrespondentId', k, true)}
            ausloeser={
              <button type="button" id={`${id}-korrespondent`} className={auswahlKnopf} disabled={nurLesen}>
                {korrespondentLogo && <KorrespondentLogo logo={korrespondentLogo} groesse={22} ton="orange" />}
                <span className="min-w-0 flex-1 truncate">{korrespondentName ?? 'Kein Korrespondent'}</span>
                {!nurLesen && <Icon name="expand_more" className="text-gedaempft" />}
              </button>
            }
          />
        </Feld>
        {istZuPruefen(beleg) && beleg.bankverbindungen.length > 0 && (
          <Feld id={`${id}-iban`} label="IBAN laut Beleg" hinweis={ibanHinweis}>
            <div className="flex flex-col gap-1.5">
              {beleg.bankverbindungen.map((b, i) => (
                <input
                  key={b.iban}
                  id={i === 0 ? `${id}-iban` : undefined}
                  aria-label={i === 0 ? undefined : `IBAN laut Beleg ${i + 1}`}
                  className="eingabe mono"
                  value={ibanFormatieren(b.iban)}
                  title={b.bic ? `BIC ${b.bic}` : undefined}
                  readOnly
                />
              ))}
            </div>
          </Feld>
        )}
        <Feld
          id={`${id}-belegart`}
          label="Belegart"
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('belegart')}
        >
          <select
            id={`${id}-belegart`}
            ref={belegartRef}
            className="eingabe"
            value={werte.belegart}
            disabled={nurLesen}
            onChange={(e) => setze('belegart', e.target.value as Belegart, true)}
          >
            {belegarten.map((art) => (
              <option key={art}>{art}</option>
            ))}
          </select>
        </Feld>
        <Feld
          id={`${id}-rechnungsnummer`}
          label="Rechnungsnummer"
          fehler={fehler.rechnungsnummer}
          onZuruecksetzen={frageStellen}
          vorher={vorherVon('rechnungsnummer', { mono: true })}
        >
          <input onChange={(e) => setze('rechnungsnummer', e.target.value)} {...eingabe('rechnungsnummer', 'mono')} />
        </Feld>
        <Feld
          id={`${id}-rechnungsdatum`}
          label="Rechnungsdatum"
          fehler={fehler.rechnungsdatum}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('rechnungsdatum')}
        >
          <Datumseingabe
            {...eingabe('rechnungsdatum')}
            wert={werte.rechnungsdatum}
            onAendern={(t) => setze('rechnungsdatum', t)}
            onGewaehlt={() => void speichern()}
          />
        </Feld>
        <Feld
          id={`${id}-leistung`}
          label="Leistungsdatum / -zeitraum"
          fehler={fehler.leistung}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('leistung')}
        >
          <Datumseingabe
            {...eingabe('leistung')}
            zeitraum
            wert={werte.leistung}
            onAendern={(t) => setze('leistung', t)}
            onGewaehlt={() => void speichern()}
          />
        </Feld>
        <Feld
          id={`${id}-faelligkeitsdatum`}
          label="Fällig am"
          fehler={fehler.faelligkeitsdatum}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('faelligkeitsdatum')}
        >
          <Datumseingabe
            {...eingabe('faelligkeitsdatum')}
            wert={werte.faelligkeitsdatum}
            onAendern={(t) => setze('faelligkeitsdatum', t)}
            onGewaehlt={() => void speichern()}
          />
        </Feld>
        <Feld
          id={`${id}-kategorie`}
          label="Kategorie"
          hinweis={kategorieHinweis}
          fehler={fehler.kategorieId}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('kategorieId', { anzeige: kategorieNameVon })}
        >
          <KategorieAuswahl
            wert={werte.kategorieId}
            onAendern={(k) => setze('kategorieId', k, true)}
            leerText={defaultName ? `Leer lassen = Default: ${defaultName}` : undefined}
            ausloeser={
              <button type="button" id={`${id}-kategorie`} className={auswahlKnopf} disabled={nurLesen}>
                <span className="min-w-0 flex-1 truncate">{kategorieName ?? 'Keine Kategorie'}</span>
                {!nurLesen && <Icon name="expand_more" className="text-gedaempft" />}
              </button>
            }
          />
        </Feld>
        <Feld
          id={`${id}-waehrung`}
          label="Währung"
          fehler={fehler.waehrung}
          onZuruecksetzen={frageStellen}
          rechtsBelegt
          vorher={vorherVon('waehrung')}
        >
          <select
            id={`${id}-waehrung`}
            className="eingabe"
            value={werte.waehrung}
            disabled={nurLesen}
            onChange={(e) => setze('waehrung', e.target.value, true)}
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
              neuAnlegenErlaubt
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

      <BetraegeBereich
        werte={werte}
        fehler={fehler}
        nurLesen={nurLesen}
        onZeile={setzeZeile}
        onZeilen={(zeilen, sofort) => setze('steuerzeilen', zeilen, sofort)}
        onKopf={(feld, wert) => setze(feld, wert, true)}
      />

      <ZahlungBereich beleg={beleg} nurLesen={nurLesen} />

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
        // Absolut zur Karte (BelegDetailSeite), nicht zum Scrollbereich: bleibt beim Scrollen in der Ecke.
        <div className="group absolute right-4 bottom-1.5 z-10 flex items-center gap-2 rounded-md py-0.5 pr-0.5 pl-2 text-xs whitespace-nowrap text-gedaempft-stark hover:bg-karte hover:shadow-sm focus-within:bg-karte focus-within:shadow-sm">
          {status === 'gespeichert' && rueckgaengig && (
            <button type="button" className={statusKnopf} onClick={() => void rueckgaengigMachen()}>
              Rückgängig
            </button>
          )}
          {status === 'fehlgeschlagen' && (
            <button type="button" className={statusKnopf} onClick={() => void speichern(true)}>
              Erneut versuchen
            </button>
          )}
          <span role="status" className={`flex items-center gap-1.5 ${statusAnzeige[status].klasse ?? ''}`}>
            <span className="sr-only group-hover:not-sr-only group-focus-within:not-sr-only">
              {statusAnzeige[status].text}
            </span>
            <Icon name={statusAnzeige[status].icon} className="text-[17px]" />
          </span>
        </div>
      )}

      <AlertDialog open={frageOffen} onOpenChange={setFrageOffen}>
        <AlertDialogContent
          onCloseAutoFocus={(e) => {
            // Der Knopf im Feld ist dann meist weg – Fokus ins Feld statt auf die Seite.
            e.preventDefault()
            if (frage) document.getElementById(frage.fokusId)?.focus()
          }}
        >
          <AlertDialogTitle>Wert zurücksetzen</AlertDialogTitle>
          <AlertDialogDescription>
            Wollen Sie {frage?.label} wirklich auf{' '}
            {frage?.text ? <strong className={cn('text-text', frage.mono && 'mono')}>„{frage.text}“</strong> : 'leer'}{' '}
            zurücksetzen?
          </AlertDialogDescription>
          <AlertDialogFooter>
            <AlertDialogCancel>Abbrechen</AlertDialogCancel>
            <AlertDialogAction onClick={() => frage && zuruecksetzen(frage.feld)}>Zurücksetzen</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      <AlertDialog open={blocker.status === 'blocked'} onOpenChange={(offen) => !offen && blocker.reset?.()}>
        <AlertDialogContent>
          <AlertDialogTitle>Änderungen nicht gespeichert</AlertDialogTitle>
          <AlertDialogDescription>
            Die Änderungen an Beleg {beleg.rechnungsnummer ?? beleg.titel} konnten nicht gespeichert werden
            {status === 'fehlgeschlagen' ? '.' : ' – einige Eingaben sind ungültig.'} Beim Verlassen gehen sie verloren.
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
