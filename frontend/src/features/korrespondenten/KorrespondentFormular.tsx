import { useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useId, useState, type FormEvent, type ReactNode } from 'react'
import { istApiFehler } from '@/api/client'
import { korrespondentenKeys, useKorrespondentAendern, useKorrespondentAnlegen } from '@/api/korrespondenten'
import { useKategorien } from '@/api/tags'
import type { KorrespondentAendern, KorrespondentDetail, KorrespondentListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { bicGueltig, bicNormalisieren, ibanFormatieren, ibanGueltig } from '@/lib/iban'

type Werte = {
  name: string
  strasse: string
  plz: string
  ort: string
  land: string
  ustIdNr: string
  steuernummer: string
  bankverbindungen: { iban: string; bic: string }[]
  website: string
  email: string
  istLieferant: boolean
  istKunde: boolean
  defaultKategorieId: string
}

type Fehler = Partial<Record<string, string>>

/** Anschrift wird als mehrzeiliger Text gespeichert: Straße / PLZ Ort / Land. */
function anschriftZerlegen(anschrift?: string | null): Pick<Werte, 'strasse' | 'plz' | 'ort' | 'land'> {
  const [strasse = '', plzOrt = '', land = ''] = (anschrift ?? '').split('\n').map((z) => z.trim())
  const treffer = /^(\S+)\s+(.*)$/.exec(plzOrt)
  return { strasse, plz: treffer?.[1] ?? '', ort: treffer?.[2] ?? plzOrt, land }
}

function anschriftZusammensetzen(w: Werte): string | null {
  const zeilen = [w.strasse.trim(), `${w.plz.trim()} ${w.ort.trim()}`.trim(), w.land.trim()]
  while (zeilen.length && !zeilen[zeilen.length - 1]) zeilen.pop()
  return zeilen.length ? zeilen.join('\n') : null
}

function startwerte(k?: KorrespondentDetail): Werte {
  return {
    name: k?.name ?? '',
    ...anschriftZerlegen(k?.anschrift),
    ustIdNr: k?.ustIdNr ?? '',
    steuernummer: k?.steuernummer ?? '',
    bankverbindungen: k?.bankverbindungen.map((b) => ({ iban: ibanFormatieren(b.iban), bic: b.bic ?? '' })) ?? [],
    website: k?.website ?? '',
    email: k?.email ?? '',
    istLieferant: k?.istLieferant ?? true,
    istKunde: k?.istKunde ?? false,
    defaultKategorieId: k?.defaultKategorie?.id ?? '',
  }
}

const leer = (text: string) => text.trim() || null

function Feld({ label, fehler, children, id }: { label: string; fehler?: string; children: ReactNode; id: string }) {
  return (
    <div>
      <label className="label" htmlFor={id}>
        {label}
      </label>
      {children}
      {fehler && (
        <span id={`${id}-fehler`} role="alert" className="hinweis text-[var(--color-rot-text)]">
          {fehler}
        </span>
      )}
    </div>
  )
}

/**
 * Dialog „Korrespondent anlegen“ bzw. „Korrespondent bearbeiten“ (mit `korrespondent`).
 * IBAN und BIC werden im Browser geprüft; Serverfehler erscheinen am Feld, 409 als Hinweis mit Link.
 */
export function KorrespondentFormular({
  korrespondent,
  onSchliessen,
  onGespeichert,
}: {
  korrespondent?: KorrespondentDetail
  onSchliessen: () => void
  onGespeichert?: (k: KorrespondentDetail) => void
}) {
  const id = useId()
  const toast = useToast()
  const queryClient = useQueryClient()
  const { data: kategorien = [] } = useKategorien()
  const anlegen = useKorrespondentAnlegen()
  const aendern = useKorrespondentAendern()
  const [werte, setWerte] = useState<Werte>(() => startwerte(korrespondent))
  const [fehler, setFehler] = useState<Fehler>({})
  const [konflikt, setKonflikt] = useState<{ ustIdNr: string; vorhandenId?: string; name?: string } | null>(null)
  const laeuft = anlegen.isPending || aendern.isPending

  const setze = <S extends keyof Werte>(schluessel: S, wert: Werte[S]) =>
    setWerte((alt) => ({ ...alt, [schluessel]: wert }))

  /** Name des vorhandenen Korrespondenten für den Linktext, falls er in einer geladenen Liste steht. */
  const nameAusListen = (vorhandenId: string) =>
    queryClient
      .getQueriesData<KorrespondentListeneintrag[]>({ queryKey: korrespondentenKeys.alle })
      .flatMap(([, liste]) => (Array.isArray(liste) ? liste : []))
      .find((k) => k.id === vorhandenId)?.name

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    const neueFehler: Fehler = {}
    if (!werte.name.trim()) neueFehler.name = 'Name ist erforderlich.'
    werte.bankverbindungen.forEach(({ iban, bic }, i) => {
      if (!ibanGueltig(iban)) neueFehler[`iban[${i}]`] = 'IBAN ist ungültig (Prüfziffer).'
      else if (bic.trim() && !bicGueltig(bic)) neueFehler[`iban[${i}]`] = 'BIC ist ungültig (8 oder 11 Stellen).'
    })
    setFehler(neueFehler)
    setKonflikt(null)
    if (Object.keys(neueFehler).length) return

    const eingabe: KorrespondentAendern = {
      name: werte.name.trim(),
      aliase: korrespondent?.aliase ?? [],
      anschrift: anschriftZusammensetzen(werte),
      ustIdNr: leer(werte.ustIdNr),
      steuernummer: leer(werte.steuernummer),
      bankverbindungen: werte.bankverbindungen.map(({ iban, bic }) => ({
        iban: iban.replace(/\s+/g, '').toUpperCase(),
        bic: bicNormalisieren(bic) || null,
      })),
      website: leer(werte.website),
      email: leer(werte.email),
      istLieferant: werte.istLieferant,
      istKunde: werte.istKunde,
      defaultKategorieId: werte.defaultKategorieId || null,
      matching: korrespondent?.matching ?? { algorithmus: 'Auto', muster: null, grossKlein: false },
    }
    const optionen = {
      onSuccess: (k: KorrespondentDetail) => {
        toast.erfolg(korrespondent ? 'Korrespondent gespeichert' : 'Korrespondent angelegt')
        onGespeichert?.(k)
        onSchliessen()
      },
      onError: (f: Error) => {
        if (istApiFehler(f) && f.status === 409) {
          const vorhandenId = typeof f.problem.korrespondentId === 'string' ? f.problem.korrespondentId : undefined
          setKonflikt({
            ustIdNr: werte.ustIdNr.trim(),
            vorhandenId,
            name: vorhandenId ? nameAusListen(vorhandenId) : undefined,
          })
        } else if (istApiFehler(f) && f.fehler) {
          const feld: Fehler = {}
          for (const [schluessel, texte] of Object.entries(f.fehler))
            feld[schluessel.charAt(0).toLowerCase() + schluessel.slice(1)] = texte.join(' ')
          setFehler(feld)
        } else toast.fehler(f.message)
      },
    }
    if (korrespondent) aendern.mutate({ id: korrespondent.id, ...eingabe }, optionen)
    else anlegen.mutate(eingabe, optionen)
  }

  const eingabe = (
    schluessel: Exclude<keyof Werte, 'bankverbindungen' | 'istLieferant' | 'istKunde'>,
    label: string,
  ) => (
    <Feld label={label} id={`${id}-${schluessel}`} fehler={fehler[schluessel]}>
      <input
        id={`${id}-${schluessel}`}
        className="eingabe"
        value={werte[schluessel]}
        aria-invalid={!!fehler[schluessel]}
        onChange={(e) => setze(schluessel, e.target.value)}
      />
    </Feld>
  )

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent className="max-w-[640px]">
        <DialogHeader>
          <DialogTitle>{korrespondent ? 'Korrespondent bearbeiten' : 'Korrespondent anlegen'}</DialogTitle>
        </DialogHeader>
        <form noValidate onSubmit={absenden} className="grid gap-3">
          {eingabe('name', 'Name')}
          {eingabe('strasse', 'Straße')}
          <div className="grid grid-cols-[120px_1fr] gap-3">
            {eingabe('plz', 'PLZ')}
            {eingabe('ort', 'Ort')}
          </div>
          {eingabe('land', 'Land')}
          <div className="grid grid-cols-2 gap-3">
            {eingabe('ustIdNr', 'USt-IdNr.')}
            {eingabe('steuernummer', 'Steuernummer')}
          </div>
          {konflikt && (
            <p role="alert" className="m-0 rounded-[8px] bg-[var(--color-orange-flaeche)] p-3 text-sm">
              Die USt-IdNr. {konflikt.ustIdNr} ist schon vergeben.{' '}
              {konflikt.vorhandenId ? (
                <Link to="/korrespondenten/$korrespondentId" params={{ korrespondentId: konflikt.vorhandenId }}>
                  {konflikt.name ? `Zu ${konflikt.name}` : 'Vorhandenen Korrespondenten öffnen'}
                </Link>
              ) : (
                <Link to="/korrespondenten" search={{ suche: konflikt.ustIdNr }}>
                  Vorhandenen Korrespondenten öffnen
                </Link>
              )}
            </p>
          )}
          <fieldset className="m-0 border-0 p-0">
            <legend className="label">Bankverbindungen</legend>
            <div className="grid gap-2">
              {werte.bankverbindungen.map((verbindung, i) => {
                const feldId = `${id}-iban-${i}`
                const feldFehler = fehler[`iban[${i}]`]
                const aendere = (teil: Partial<Werte['bankverbindungen'][number]>) =>
                  setze(
                    'bankverbindungen',
                    werte.bankverbindungen.map((x, j) => (j === i ? { ...x, ...teil } : x)),
                  )
                return (
                  <div key={i}>
                    <div className="flex gap-2">
                      <input
                        id={feldId}
                        className="eingabe mono"
                        aria-label={`IBAN ${i + 1}`}
                        aria-invalid={!!feldFehler}
                        placeholder="IBAN"
                        value={verbindung.iban}
                        onChange={(e) => aendere({ iban: e.target.value })}
                        onBlur={() => aendere({ iban: ibanFormatieren(verbindung.iban) })}
                      />
                      <input
                        className="eingabe mono w-[150px] shrink-0"
                        aria-label={`BIC ${i + 1}`}
                        aria-invalid={!!feldFehler}
                        placeholder="BIC (optional)"
                        value={verbindung.bic}
                        onChange={(e) => aendere({ bic: e.target.value })}
                        onBlur={() => aendere({ bic: bicNormalisieren(verbindung.bic) })}
                      />
                      <Button
                        size="icon"
                        aria-label={`Bankverbindung ${i + 1} entfernen`}
                        onClick={() =>
                          setze(
                            'bankverbindungen',
                            werte.bankverbindungen.filter((_, j) => j !== i),
                          )
                        }
                      >
                        <Icon name="delete" />
                      </Button>
                    </div>
                    {feldFehler && (
                      <span role="alert" className="hinweis text-[var(--color-rot-text)]">
                        {feldFehler}
                      </span>
                    )}
                  </div>
                )
              })}
              {fehler.bankverbindungen && (
                <span role="alert" className="hinweis text-[var(--color-rot-text)]">
                  {fehler.bankverbindungen}
                </span>
              )}
              <Button
                size="klein"
                className="justify-self-start"
                onClick={() => setze('bankverbindungen', [...werte.bankverbindungen, { iban: '', bic: '' }])}
              >
                <Icon name="add" />
                Bankverbindung hinzufügen
              </Button>
            </div>
          </fieldset>
          <div className="grid grid-cols-2 gap-3">
            {eingabe('website', 'Website')}
            {eingabe('email', 'E-Mail')}
          </div>
          <fieldset className="m-0 border-0 p-0">
            <legend className="label">Typ</legend>
            <div className="flex gap-4">
              {/* Native Checkboxen: Radix-Checkboxen legen im Formular ein verstecktes Eingabefeld an. */}
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-[18px] accent-[var(--color-akzent)]"
                  checked={werte.istLieferant}
                  onChange={(e) => setze('istLieferant', e.target.checked)}
                />
                Lieferant
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  className="size-[18px] accent-[var(--color-akzent)]"
                  checked={werte.istKunde}
                  onChange={(e) => setze('istKunde', e.target.checked)}
                />
                Kunde
              </label>
            </div>
          </fieldset>
          <Feld label="Default-Kategorie" id={`${id}-kategorie`} fehler={fehler.defaultKategorieId}>
            <select
              id={`${id}-kategorie`}
              className="eingabe"
              value={werte.defaultKategorieId}
              onChange={(e) => setze('defaultKategorieId', e.target.value)}
            >
              <option value="">Keine</option>
              {kategorien.map((k) => (
                <option key={k.id} value={k.id}>
                  {k.name}
                </option>
              ))}
            </select>
          </Feld>
          <DialogFooter>
            <Button onClick={onSchliessen}>Abbrechen</Button>
            <Button type="submit" variant="primaer" laedt={laeuft}>
              {korrespondent ? 'Speichern' : 'Anlegen'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
