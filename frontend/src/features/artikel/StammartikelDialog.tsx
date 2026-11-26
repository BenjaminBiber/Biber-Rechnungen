import { useNavigate } from '@tanstack/react-router'
import { useId, useState, type FormEvent } from 'react'
import { useStammartikelAendern, useStammartikelAnlegen, useStammartikelDetail } from '@/api/artikel'
import type { ArtikelListeneintrag, ArtikelZuordnung, PreisDetail, StammartikelAendern } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { SkelettFormular } from '@/components/ui/skelett'
import { formatEuro, formatZahl } from '@/lib/format'
import { grundpreis } from './grundpreis'
import { kurzname } from './suche'

export const BASISEINHEITEN = ['Blatt', 'Stück', 'kg', 'l', 'm', 'Seiten', 'Monat']
const FREI = '__frei__'

const feld =
  'h-[34px] w-full rounded-[7px] border border-eingabe-rand bg-karte px-2.5 text-sm text-text outline-none focus:border-akzent'

/** Dezimalzahl aus deutscher Eingabe („2.500“, „0,5“); ungültig → NaN. */
function zahl(text: string): number {
  const bereinigt = text.trim().replace(/\./g, '').replace(',', '.')
  return bereinigt ? Number(bereinigt) : NaN
}

/**
 * „Stammartikel bilden“: legt aus den gewählten Artikeln einen Stammartikel an. Hat einer der Artikel
 * schon einen Stammartikel, werden die übrigen stattdessen diesem zugeordnet.
 */
export function StammartikelDialog({
  artikel,
  offen,
  onSchliessen,
}: {
  artikel: ArtikelListeneintrag[]
  offen: boolean
  onSchliessen: () => void
}) {
  return (
    <Dialog open={offen} onOpenChange={(o) => !o && onSchliessen()}>
      <DialogContent className="max-w-[560px]">
        {offen && <Auswahl artikel={artikel} onSchliessen={onSchliessen} />}
      </DialogContent>
    </Dialog>
  )
}

type InhaltProps = { artikel: ArtikelListeneintrag[]; onSchliessen: () => void }

function Auswahl(props: InhaltProps) {
  const vorhanden = props.artikel.find((a) => a.stammartikel)?.stammartikel
  return vorhanden ? <MitStammartikel id={vorhanden.id} {...props} /> : <Inhalt {...props} />
}

/** Lädt den vorhandenen Stammartikel, damit seine bisherigen Zuordnungen erhalten bleiben. */
function MitStammartikel({ id, ...props }: InhaltProps & { id: string }) {
  const { data, isPending } = useStammartikelDetail(id)
  if (isPending)
    return (
      <div className="grid gap-4">
        <DialogHeader>
          <DialogTitle>Stammartikel zuordnen</DialogTitle>
        </DialogHeader>
        <SkelettFormular felder={2} spalten={1} label="Stammartikel wird geladen" />
      </div>
    )
  return <Inhalt {...props} stamm={data} />
}

function Inhalt({ artikel, onSchliessen, stamm }: InhaltProps & { stamm?: PreisDetail }) {
  const vorhanden = artikel.find((a) => a.stammartikel)?.stammartikel ?? null
  const neue = vorhanden ? artikel.filter((a) => a.stammartikel?.id !== vorhanden.id) : artikel
  const anlegen = useStammartikelAnlegen()
  const aendern = useStammartikelAendern()
  const navigate = useNavigate()
  const toast = useToast()
  const id = useId()

  const [name, setName] = useState(artikel[0]?.name ?? '')
  const [einheitWahl, setEinheitWahl] = useState(BASISEINHEITEN[0])
  const [freieEinheit, setFreieEinheit] = useState('')
  const [mengeText, setMengeText] = useState('1')
  const [faktoren, setFaktoren] = useState<Record<string, string>>({})
  const [fehler, setFehler] = useState<string | null>(null)

  const basiseinheit = stamm?.basiseinheit ?? (einheitWahl === FREI ? freieEinheit.trim() : einheitWahl)
  const menge = stamm?.grundpreisMenge ?? zahl(mengeText)
  const faktorVon = (a: ArtikelListeneintrag) => zahl(faktoren[a.id] ?? '')

  const vorschau = neue
    .filter((a) => a.letzterEinzelpreis != null && faktorVon(a) > 0 && menge > 0)
    .map(
      (a) => `${kurzname(a.korrespondent.name)} ${formatEuro(grundpreis(a.letzterEinzelpreis!, faktorVon(a), menge))}`,
    )

  const laeuft = anlegen.isPending || aendern.isPending

  async function absenden(e: FormEvent) {
    e.preventDefault()
    if (!vorhanden && !name.trim()) return setFehler('Bitte einen Namen angeben.')
    if (!basiseinheit) return setFehler('Bitte eine Basiseinheit angeben.')
    if (!(menge > 0)) return setFehler('„Grundpreis je“ muss größer als 0 sein.')
    if (neue.some((a) => !(faktorVon(a) > 0))) return setFehler('Jeder Umrechnungsfaktor muss größer als 0 sein.')
    if (vorhanden && !stamm) return setFehler('Der Stammartikel wird noch geladen.')
    setFehler(null)

    const zuordnungen: ArtikelZuordnung[] = neue.map((a) => ({ artikelId: a.id, umrechnungFaktor: faktorVon(a) }))
    try {
      if (vorhanden && stamm) {
        const eingabe: StammartikelAendern = {
          name: stamm.name,
          basiseinheit: stamm.basiseinheit ?? basiseinheit,
          grundpreisMenge: stamm.grundpreisMenge ?? menge,
          artikel: [
            ...stamm.artikel.map((a) => ({ artikelId: a.id, umrechnungFaktor: a.umrechnungFaktor })),
            ...zuordnungen,
          ],
        }
        await aendern.mutateAsync({ id: vorhanden.id, ...eingabe })
        toast.erfolg(`${neue.length} Artikel „${stamm.name}“ zugeordnet`)
        onSchliessen()
        void navigate({ to: '/artikel/stammartikel/$stammartikelId', params: { stammartikelId: vorhanden.id } })
      } else {
        const neu = await anlegen.mutateAsync({
          name: name.trim(),
          basiseinheit,
          grundpreisMenge: menge,
          artikel: zuordnungen,
        })
        toast.erfolg(`Stammartikel „${neu.name}“ angelegt`)
        onSchliessen()
        void navigate({ to: '/artikel/stammartikel/$stammartikelId', params: { stammartikelId: neu.id } })
      }
    } catch {
      setFehler('Der Stammartikel konnte nicht gespeichert werden.')
    }
  }

  const einheitText = basiseinheit || 'Einheit'
  const mengeAnzeige = menge > 0 ? formatZahl(menge) : '…'

  return (
    <form onSubmit={(e) => void absenden(e)} noValidate className="grid gap-4">
      <DialogHeader>
        <DialogTitle>{vorhanden ? 'Stammartikel zuordnen' : 'Stammartikel bilden'}</DialogTitle>
        {vorhanden && (
          <DialogDescription>
            „{artikel.find((a) => a.stammartikel)?.name}“ gehört schon zum Stammartikel „{vorhanden.name}“. Die übrigen
            Artikel diesem zuordnen?
          </DialogDescription>
        )}
      </DialogHeader>

      {!vorhanden && (
        <>
          <label className="grid gap-1 text-sm font-medium">
            Name
            <input className={feld} value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="grid gap-1 text-sm font-medium">
              Basiseinheit
              <select className={feld} value={einheitWahl} onChange={(e) => setEinheitWahl(e.target.value)}>
                {BASISEINHEITEN.map((b) => (
                  <option key={b} value={b}>
                    {b}
                  </option>
                ))}
                <option value={FREI}>Andere …</option>
              </select>
            </label>
            <label className="grid gap-1 text-sm font-medium">
              Grundpreis je
              <input
                className={feld}
                inputMode="decimal"
                value={mengeText}
                onChange={(e) => setMengeText(e.target.value)}
              />
            </label>
          </div>
          {einheitWahl === FREI && (
            <label className="grid gap-1 text-sm font-medium">
              Eigene Basiseinheit
              <input className={feld} value={freieEinheit} onChange={(e) => setFreieEinheit(e.target.value)} />
            </label>
          )}
        </>
      )}

      <fieldset className="m-0 grid gap-2 border-0 p-0">
        <legend className="mb-1 text-sm font-medium">Umrechnung je Artikel</legend>
        {neue.map((a) => (
          <div key={a.id} className="flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="min-w-0">
              {a.name} <span className="gedaempft">· {kurzname(a.korrespondent.name)}</span>
            </span>
            <span className="flex items-center gap-1.5 whitespace-nowrap">
              <label htmlFor={`${id}-${a.id}`}>1 {a.einheit ?? 'Einheit'} =</label>
              <input
                id={`${id}-${a.id}`}
                aria-label={`Faktor ${a.name}`}
                className={`${feld} w-24 text-right`}
                inputMode="decimal"
                value={faktoren[a.id] ?? ''}
                onChange={(e) => setFaktoren((f) => ({ ...f, [a.id]: e.target.value }))}
              />
              {einheitText}
            </span>
          </div>
        ))}
      </fieldset>

      <p className="m-0 flex items-center gap-2 rounded-[7px] bg-akzent-zart px-3 py-2 text-sm" role="status">
        <Icon name="calculate" />
        {vorschau.length > 0
          ? `Grundpreis je ${mengeAnzeige} ${einheitText}: ${vorschau.join(' · ')}`
          : `Grundpreis je ${mengeAnzeige} ${einheitText}: Faktoren eingeben`}
      </p>

      {fehler && (
        <p role="alert" className="m-0 text-sm text-rot-text">
          {fehler}
        </p>
      )}

      <DialogFooter>
        <button type="button" className="btn" onClick={onSchliessen}>
          Abbrechen
        </button>
        <button type="submit" className="btn btn-primaer" disabled={laeuft} aria-busy={laeuft || undefined}>
          {vorhanden ? 'Zuordnen' : 'Stammartikel anlegen'}
        </button>
      </DialogFooter>
    </form>
  )
}
