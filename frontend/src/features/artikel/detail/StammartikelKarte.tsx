import { useState } from 'react'
import { useArtikel, useStammartikelAendern } from '@/api/artikel'
import type { PreisDetail, ZugeordneterArtikel } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { formatZahl } from '@/lib/format'
import { einheitKurz } from './gemeinsam'

type Zeile = Pick<ZugeordneterArtikel, 'id' | 'name' | 'einheit'> & { korrespondent: string; faktor: string }

const zahlAus = (text: string) => Number(text.replace(/\./g, '').replace(',', '.'))

/** Suche nach Artikeln ohne Stammartikel und Eingabe des Faktors. */
function ArtikelZuordnen({ basiseinheit, onZuordnen }: { basiseinheit: string; onZuordnen: (z: Zeile) => void }) {
  const [suche, setSuche] = useState('')
  const [gewaehlt, setGewaehlt] = useState<string>()
  const [faktor, setFaktor] = useState('')
  const { data } = useArtikel({ suche: suche || undefined, ohneStammartikel: true })
  const treffer = data?.eintraege ?? []
  const artikel = treffer.find((a) => a.id === gewaehlt)
  return (
    <div className="mt-3 flex flex-col gap-2 rounded-[9px] border border-rand p-3">
      <label className="flex flex-col gap-1 text-[13px]">
        Artikel suchen
        <input type="search" className="eingabe" value={suche} onChange={(e) => setSuche(e.target.value)} />
      </label>
      <ul className="m-0 flex max-h-40 list-none flex-col gap-1 overflow-y-auto p-0" aria-label="Treffer">
        {treffer.map((a) => (
          <li key={a.id}>
            <button
              type="button"
              className="btn btn-klein w-full justify-start"
              aria-pressed={a.id === gewaehlt}
              onClick={() => setGewaehlt(a.id)}
            >
              {a.name} · {a.korrespondent.name}
            </button>
          </li>
        ))}
      </ul>
      {artikel && (
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-[13px]">
            1 {artikel.einheit ?? 'Stück'} = … {einheitKurz(basiseinheit)}
            <input
              className="eingabe w-32"
              inputMode="decimal"
              value={faktor}
              onChange={(e) => setFaktor(e.target.value)}
            />
          </label>
          <Button
            size="klein"
            disabled={!(zahlAus(faktor) > 0)}
            onClick={() =>
              onZuordnen({
                id: artikel.id,
                name: artikel.name,
                einheit: artikel.einheit,
                korrespondent: artikel.korrespondent.name,
                faktor,
              })
            }
          >
            Zuordnen
          </Button>
        </div>
      )}
    </div>
  )
}

/** Karte „Stammartikel“: Stammdaten, zugeordnete Artikel mit Umrechnungsfaktor, Zuordnen und Speichern. */
export function StammartikelKarte({ detail, darfBearbeiten }: { detail: PreisDetail; darfBearbeiten: boolean }) {
  const toast = useToast()
  const aendern = useStammartikelAendern()
  const [name, setName] = useState(detail.name)
  const [basiseinheit, setBasiseinheit] = useState(detail.basiseinheit ?? '')
  const [menge, setMenge] = useState(formatZahl(detail.grundpreisMenge ?? 1))
  const [zeilen, setZeilen] = useState<Zeile[]>(() =>
    detail.artikel.map((a) => ({
      id: a.id,
      name: a.name,
      einheit: a.einheit,
      korrespondent: a.korrespondent.name,
      faktor: a.umrechnungFaktor != null ? formatZahl(a.umrechnungFaktor) : '',
    })),
  )
  const [zuordnen, setZuordnen] = useState(false)
  const kurz = einheitKurz(basiseinheit)

  const speichern = () =>
    aendern.mutate(
      {
        id: detail.id,
        name: name.trim(),
        basiseinheit: basiseinheit.trim(),
        grundpreisMenge: zahlAus(menge),
        artikel: zeilen.map((z) => ({ artikelId: z.id, umrechnungFaktor: z.faktor ? zahlAus(z.faktor) : null })),
      },
      {
        onSuccess: () => toast.erfolg('Stammartikel gespeichert'),
        onError: (f: Error) => toast.fehler(f.message),
      },
    )

  return (
    <section className="karte p-5" aria-labelledby="stammartikel-titel">
      <h2 id="stammartikel-titel" className="m-0 mb-3 text-base font-semibold">
        Stammartikel
      </h2>
      {darfBearbeiten ? (
        <div className="mb-4 grid gap-3 sm:grid-cols-3">
          <label className="flex flex-col gap-1 text-[13px]">
            Name
            <input className="eingabe" value={name} onChange={(e) => setName(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 text-[13px]">
            Basiseinheit
            <input className="eingabe" value={basiseinheit} onChange={(e) => setBasiseinheit(e.target.value)} />
          </label>
          <label className="flex flex-col gap-1 text-[13px]">
            Grundpreis je
            <input className="eingabe" inputMode="decimal" value={menge} onChange={(e) => setMenge(e.target.value)} />
          </label>
        </div>
      ) : (
        <dl className="mb-4 grid gap-3 text-[13px] sm:grid-cols-3">
          <div>
            <dt className="gedaempft">Name</dt>
            <dd className="m-0">{detail.name}</dd>
          </div>
          <div>
            <dt className="gedaempft">Basiseinheit</dt>
            <dd className="m-0">{detail.basiseinheit}</dd>
          </div>
          <div>
            <dt className="gedaempft">Grundpreis je</dt>
            <dd className="m-0">
              {formatZahl(detail.grundpreisMenge ?? 1)} {einheitKurz(detail.basiseinheit)}
            </dd>
          </div>
        </dl>
      )}
      <h3 className="m-0 mb-2 text-sm font-semibold">Zugeordnete Artikel und Umrechnung</h3>
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {zeilen.map((z, i) => (
          <li key={z.id} className="flex flex-wrap items-center justify-between gap-2 border-b border-linie pb-2">
            <span className="min-w-0">
              <span className="block">{z.name}</span>
              <span className="gedaempft text-xs">{z.korrespondent}</span>
            </span>
            {darfBearbeiten ? (
              <label className="flex items-center gap-1.5 text-[13px]">
                1 {z.einheit ?? 'Stück'} =
                <input
                  className="eingabe w-24 text-right"
                  inputMode="decimal"
                  aria-label={`Umrechnung ${z.name}`}
                  value={z.faktor}
                  onChange={(e) =>
                    setZeilen((alt) => alt.map((x, j) => (j === i ? { ...x, faktor: e.target.value } : x)))
                  }
                />
                {kurz}
              </label>
            ) : (
              <span className="num text-[13px]">
                1 {z.einheit ?? 'Stück'} = {z.faktor} {kurz}
              </span>
            )}
          </li>
        ))}
      </ul>
      {darfBearbeiten && (
        <>
          {zuordnen ? (
            <ArtikelZuordnen
              basiseinheit={basiseinheit}
              onZuordnen={(z) => {
                setZeilen((alt) => [...alt.filter((x) => x.id !== z.id), z])
                setZuordnen(false)
              }}
            />
          ) : (
            <Button variant="geist" size="klein" className="mt-3" onClick={() => setZuordnen(true)}>
              + Artikel zuordnen
            </Button>
          )}
          <div className="mt-4 flex justify-end">
            <Button variant="primaer" disabled={aendern.isPending} onClick={speichern}>
              <Icon name="save" />
              Speichern
            </Button>
          </div>
        </>
      )}
    </section>
  )
}
