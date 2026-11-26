import { useId, useState, type FormEvent } from 'react'
import { istApiFehler } from '@/api/client'
import {
  useKategorieAendern,
  useKategorieAnlegen,
  useKategorieLoeschen,
  useTagAendern,
  useTagAnlegen,
  useTagLoeschen,
} from '@/api/tags'
import type { Kategorie, Matching, Tag } from '@/api/typen'
import { MatchingEditor } from '@/components/auswahl/MatchingEditor'
import { useToast } from '@/components/toast'
import { Button, buttonVariants } from '@/components/ui/button'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { farben } from './gemeinsam'

type Sonderform = 'keine' | 'inbox' | 'zahlung'

type Werte = {
  name: string
  farbe: string
  sonderform: Sonderform
  euerZeile: string
  /** Oberkategorie; leer = Hauptkategorie. */
  elternId: string
  matching: Matching
}

const keinMatching: Matching = { algorithmus: 'Keiner', muster: null, grossKlein: false }

/** „3 Belege und 1 Korrespondent ohne Kategorie“ nach dem Löschen einer Kategorie. */
function betroffen(belege: number, korrespondenten: number): string {
  const b = `${belege} ${belege === 1 ? 'Beleg' : 'Belege'}`
  const k = `${korrespondenten} ${korrespondenten === 1 ? 'Korrespondent' : 'Korrespondenten'}`
  return `${b} und ${k} ohne Kategorie`
}

type Ziel =
  { art: 'tag'; tag?: Tag; alleTags: Tag[] } | { art: 'kategorie'; kategorie?: Kategorie; alleKategorien: Kategorie[] }

function startwerte(ziel: Ziel): Werte {
  if (ziel.art === 'tag') {
    const t = ziel.tag
    return {
      name: t?.name ?? '',
      farbe: t?.farbe ?? farben[0].ton,
      sonderform: t?.istInbox ? 'inbox' : t?.istZahlungsTag ? 'zahlung' : 'keine',
      euerZeile: '',
      elternId: '',
      matching: t?.matching ?? keinMatching,
    }
  }
  const k = ziel.kategorie
  return {
    name: k?.name ?? '',
    farbe: k?.farbe ?? farben[0].hex,
    sonderform: 'keine',
    euerZeile: k?.euerZeile ?? '',
    elternId: k?.elternId ?? '',
    matching: k?.matching ?? keinMatching,
  }
}

/** Dialog „Tag bearbeiten“ / „Neuer Tag“ / „Kategorie bearbeiten“ / „Neue Kategorie“. */
export function BearbeitenDialog({ onSchliessen, ...ziel }: Ziel & { onSchliessen: () => void }) {
  const id = useId()
  const toast = useToast()
  const [werte, setWerte] = useState<Werte>(() => startwerte(ziel))
  const [nameFehler, setNameFehler] = useState<string | null>(null)
  const [rueckfrage, setRueckfrage] = useState(false)
  const tagAnlegen = useTagAnlegen()
  const tagAendern = useTagAendern()
  const tagLoeschen = useTagLoeschen()
  const katAnlegen = useKategorieAnlegen()
  const katAendern = useKategorieAendern()
  const katLoeschen = useKategorieLoeschen()

  const istTag = ziel.art === 'tag'
  const vorhanden = ziel.art === 'tag' ? ziel.tag : ziel.kategorie
  const titel = istTag
    ? vorhanden
      ? 'Tag bearbeiten'
      : 'Neuer Tag'
    : vorhanden
      ? 'Kategorie bearbeiten'
      : 'Neue Kategorie'
  const laeuft = [tagAnlegen, tagAendern, katAnlegen, katAendern].some((m) => m.isPending)
  const setze = <S extends keyof Werte>(schluessel: S, wert: Werte[S]) =>
    setWerte((alt) => ({ ...alt, [schluessel]: wert }))

  // Nur eine Ebene: Oberkategorie kann nur eine Hauptkategorie sein, und wer Unterkategorien hat, bleibt Hauptkategorie.
  const moeglicheEltern =
    ziel.art === 'kategorie'
      ? ziel.alleKategorien
          .filter((k) => !k.elternId && k.id !== ziel.kategorie?.id)
          .sort((a, b) => a.name.localeCompare(b.name, 'de', { sensitivity: 'base' }))
      : []
  const unterkategorien =
    ziel.art === 'kategorie' && ziel.kategorie
      ? ziel.alleKategorien.filter((k) => k.elternId === ziel.kategorie?.id)
      : []
  const elternEuerZeile = moeglicheEltern.find((k) => k.id === werte.elternId)?.euerZeile

  const bisherigerInbox =
    ziel.art === 'tag' && werte.sonderform === 'inbox'
      ? ziel.alleTags.find((t) => t.istInbox && t.id !== ziel.tag?.id)
      : undefined

  const beiFehler = (f: Error) => {
    if (istApiFehler(f) && f.status === 409) setNameFehler('Name ist bereits vergeben')
    else if (istApiFehler(f) && f.fehler) {
      const name = Object.entries(f.fehler).find(([s]) => s.toLowerCase() === 'name')
      if (name) setNameFehler(name[1].join(' '))
      else toast.fehler(Object.values(f.fehler).flat().join(' '))
    } else toast.fehler(f.message)
  }

  const absenden = (e: FormEvent) => {
    e.preventDefault()
    const name = werte.name.trim()
    if (!name) return setNameFehler('Name ist erforderlich.')
    setNameFehler(null)
    const optionen = {
      onSuccess: () => {
        toast.erfolg(`${istTag ? 'Tag' : 'Kategorie'} ${vorhanden ? 'gespeichert' : 'angelegt'}`)
        onSchliessen()
      },
      onError: beiFehler,
    }
    if (ziel.art === 'tag') {
      const eingabe = {
        name,
        farbe: werte.farbe,
        istInbox: werte.sonderform === 'inbox',
        istZahlungsTag: werte.sonderform === 'zahlung',
        matching: werte.matching,
      }
      if (ziel.tag) tagAendern.mutate({ id: ziel.tag.id, ...eingabe }, optionen)
      else tagAnlegen.mutate(eingabe, optionen)
    } else {
      const eingabe = {
        name,
        farbe: werte.farbe,
        euerZeile: werte.euerZeile.trim() || null,
        elternId: werte.elternId || null,
        matching: werte.matching,
      }
      if (ziel.kategorie) katAendern.mutate({ id: ziel.kategorie.id, ...eingabe }, optionen)
      else katAnlegen.mutate(eingabe, optionen)
    }
  }

  const loeschen = () => {
    if (!vorhanden) return
    const onError = (f: Error) => toast.fehler(f.message)
    if (istTag)
      tagLoeschen.mutate(vorhanden.id, {
        onSuccess: () => {
          toast.erfolg('Tag gelöscht')
          onSchliessen()
        },
        onError,
      })
    else
      katLoeschen.mutate(vorhanden.id, {
        onSuccess: ({ belege, korrespondenten }) => {
          toast.erfolg(`Kategorie gelöscht – ${betroffen(belege, korrespondenten)}`)
          onSchliessen()
        },
        onError,
      })
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent className="max-w-[560px]">
        <DialogHeader>
          <DialogTitle>{titel}</DialogTitle>
        </DialogHeader>
        <form noValidate onSubmit={absenden} className="grid gap-4">
          <div>
            <label className="label" htmlFor={`${id}-name`}>
              Name
            </label>
            <input
              id={`${id}-name`}
              className="eingabe"
              value={werte.name}
              aria-invalid={!!nameFehler}
              aria-describedby={nameFehler ? `${id}-name-fehler` : undefined}
              onChange={(e) => setze('name', e.target.value)}
            />
            {nameFehler && (
              <span id={`${id}-name-fehler`} role="alert" className="hinweis text-[var(--color-rot-text)]">
                {nameFehler}
              </span>
            )}
          </div>
          <div>
            <span className="label" id={`${id}-farbe`}>
              Farbe
            </span>
            <div role="radiogroup" aria-labelledby={`${id}-farbe`} className="flex flex-wrap gap-2">
              {farben.map((f) => {
                const wert = istTag ? f.ton : f.hex
                const gewaehlt = werte.farbe.toLowerCase() === wert.toLowerCase()
                return (
                  <button
                    key={f.name}
                    type="button"
                    role="radio"
                    aria-checked={gewaehlt}
                    aria-label={f.name}
                    className="size-[30px] cursor-pointer rounded-full border-2"
                    style={{ background: f.hex, borderColor: gewaehlt ? 'var(--color-text)' : 'var(--color-karte)' }}
                    onClick={() => setze('farbe', wert)}
                  />
                )
              })}
            </div>
          </div>
          {istTag ? (
            <fieldset className="m-0 border-0 p-0">
              <legend className="label">Sonderform</legend>
              <div className="flex flex-col gap-1.5">
                {(
                  [
                    ['keine', 'Keine', null],
                    ['inbox', 'Inbox-Tag', 'wird jedem neuen Beleg automatisch gesetzt'],
                    ['zahlung', 'Zahlungs-Tag', 'markiert als bezahlt – ohne Datum; offen = kein Zahlungs-Tag'],
                  ] as const
                ).map(([wert, text, erklaerung]) => (
                  <label
                    key={wert}
                    className="flex cursor-pointer items-start gap-2.5 rounded-[8px] border border-rand px-2.5 py-2 text-[13px]"
                  >
                    <input
                      type="radio"
                      name={`${id}-sonderform`}
                      checked={werte.sonderform === wert}
                      onChange={() => setze('sonderform', wert)}
                    />
                    {erklaerung ? (
                      <span>
                        <strong>{text}</strong>
                        <br />
                        <span className="gedaempft">{erklaerung}</span>
                      </span>
                    ) : (
                      text
                    )}
                  </label>
                ))}
              </div>
              {bisherigerInbox && (
                <p role="status" className="hinweis m-0 mt-1.5">
                  Ersetzt „{bisherigerInbox.name}“ als Inbox-Tag
                </p>
              )}
            </fieldset>
          ) : (
            <>
              <div>
                <label className="label" htmlFor={`${id}-eltern`}>
                  Oberkategorie
                </label>
                <select
                  id={`${id}-eltern`}
                  className="eingabe"
                  value={werte.elternId}
                  disabled={unterkategorien.length > 0}
                  aria-describedby={unterkategorien.length > 0 ? `${id}-eltern-hinweis` : undefined}
                  onChange={(e) => setze('elternId', e.target.value)}
                >
                  <option value="">Keine – Hauptkategorie</option>
                  {moeglicheEltern.map((k) => (
                    <option key={k.id} value={k.id}>
                      {k.name}
                    </option>
                  ))}
                </select>
                {unterkategorien.length > 0 && (
                  <span id={`${id}-eltern-hinweis`} className="hinweis">
                    Hat{' '}
                    {unterkategorien.length === 1 ? 'eine Unterkategorie' : `${unterkategorien.length} Unterkategorien`}{' '}
                    und bleibt deshalb Hauptkategorie.
                  </span>
                )}
              </div>
              <div>
                <label className="label" htmlFor={`${id}-euer`}>
                  EÜR-Zeile (optional)
                </label>
                <input
                  id={`${id}-euer`}
                  className="eingabe"
                  value={werte.euerZeile}
                  placeholder={elternEuerZeile ? `${elternEuerZeile} (von der Oberkategorie)` : undefined}
                  onChange={(e) => setze('euerZeile', e.target.value)}
                />
                <span className="hinweis">
                  Wird in „Summen je Kategorie“ im Export mit ausgegeben
                  {werte.elternId ? '; leer = EÜR-Zeile der Oberkategorie' : ''}.
                </span>
              </div>
            </>
          )}
          <div className="grid gap-2.5 border-t border-linie pt-4">
            <span className="label m-0">Matching – automatisch zuweisen, wenn …</span>
            <MatchingEditor
              wert={werte.matching}
              onAendern={(m) => setze('matching', m)}
              ziel={istTag ? 'Tag' : 'Kategorie'}
              zielId={vorhanden?.id}
              zielName={werte.name || undefined}
            />
          </div>
          <DialogFooter className="flex-wrap justify-between">
            {vorhanden ? (
              <Button className="text-[var(--color-rot-text)]" onClick={() => setRueckfrage(true)}>
                Löschen
              </Button>
            ) : (
              <span />
            )}
            <span className="flex gap-2">
              <Button onClick={onSchliessen}>Abbrechen</Button>
              <Button type="submit" variant="primaer" laedt={laeuft}>
                Speichern
              </Button>
            </span>
          </DialogFooter>
        </form>
        {vorhanden && (
          <AlertDialog open={rueckfrage} onOpenChange={setRueckfrage}>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>
                  {istTag ? 'Tag' : 'Kategorie'} „{vorhanden.name}“ löschen?
                </AlertDialogTitle>
                <AlertDialogDescription>
                  {vorhanden.anzahlBelege === 1
                    ? '1 Beleg ist betroffen.'
                    : `${vorhanden.anzahlBelege} Belege sind betroffen.`}{' '}
                  {istTag ? 'Der Tag wird von ihnen entfernt.' : 'Sie verlieren ihre Kategorie.'}
                  {unterkategorien.length > 0 &&
                    ` ${unterkategorien.length === 1 ? 'Die Unterkategorie wird' : 'Die Unterkategorien werden'} zur Hauptkategorie.`}
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Abbrechen</AlertDialogCancel>
                <AlertDialogAction className={buttonVariants({ variant: 'gefaehrlich' })} onClick={loeschen}>
                  Löschen
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </DialogContent>
    </Dialog>
  )
}
