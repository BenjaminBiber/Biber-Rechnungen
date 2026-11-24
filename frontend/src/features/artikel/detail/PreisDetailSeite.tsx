import { Link } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { useArtikel, useStammartikelArtikelEntfernen } from '@/api/artikel'
import type { PreisDetail } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
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
import { Button } from '@/components/ui/button'
import { formatEuro, formatProzent, formatZahl } from '@/lib/format'
import { einheitKurz, grundpreisBezug, hatGrundpreis, type Preisart } from './gemeinsam'
import { Kaeufe } from './Kaeufe'
import { Preisverlauf } from './Preisverlauf'
import { StammartikelKarte } from './StammartikelKarte'

function Kennzahl({ titel, wert, zusatz }: { titel: string; wert: ReactNode; zusatz?: ReactNode }) {
  return (
    <div className="karte flex-[1_1_200px] p-4">
      <div className="gedaempft text-[13px]">{titel}</div>
      <div className="num mt-1 text-[22px] font-semibold">{wert}</div>
      {zusatz && <div className="gedaempft mt-0.5 text-xs">{zusatz}</div>}
    </div>
  )
}

function Kennzahlen({ detail, jahr, mitGrundpreis }: { detail: PreisDetail; jahr: number; mitGrundpreis: boolean }) {
  const k = detail.kennzahlen
  const letzterEinzelpreis = detail.kaeufe[0]?.einzelpreis
  const euro = (v?: number | null) => (v != null ? formatEuro(v) : '–')
  return (
    <div className="mb-4 flex flex-wrap gap-4" aria-label="Kennzahlen" role="group">
      {mitGrundpreis ? (
        <Kennzahl
          titel="Aktuell günstigster Grundpreis"
          wert={euro(k.guenstigsterGrundpreis)}
          zusatz={[k.guenstigsterBei, `je ${grundpreisBezug(detail)}`].filter(Boolean).join(' · ')}
        />
      ) : (
        <Kennzahl titel="Aktueller Einzelpreis" wert={euro(letzterEinzelpreis)} zusatz={k.guenstigsterBei} />
      )}
      <Kennzahl
        titel={`Änderung seit Jan ${jahr}`}
        wert={k.aenderungSeitJahresbeginn != null ? formatProzent(k.aenderungSeitJahresbeginn / 100, true) : '–'}
        zusatz={k.preisVon != null && k.preisBis != null ? `${euro(k.preisVon)} → ${euro(k.preisBis)}` : undefined}
      />
      <Kennzahl
        titel={`Menge ${jahr}`}
        wert={`${formatZahl(k.mengeJahr)} ${einheitKurz(detail.basiseinheit)}`.trim()}
      />
      <Kennzahl titel={`Ausgaben ${jahr}`} wert={formatEuro(k.ausgabenJahrNetto)} zusatz="netto" />
    </div>
  )
}

/** Auswahl eines Artikels und Rückfrage vor dem Herauslösen aus dem Stammartikel. */
function Herausloesen({ detail }: { detail: PreisDetail }) {
  const toast = useToast()
  const entfernen = useStammartikelArtikelEntfernen()
  const [offen, setOffen] = useState(false)
  const [artikelId, setArtikelId] = useState(detail.artikel[0]?.id ?? '')
  const artikel = detail.artikel.find((a) => a.id === artikelId)
  return (
    <AlertDialog open={offen} onOpenChange={setOffen}>
      <Button disabled={detail.artikel.length === 0} onClick={() => setOffen(true)}>
        <Icon name="call_split" />
        Artikel herauslösen
      </Button>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Artikel herauslösen</AlertDialogTitle>
          <AlertDialogDescription>
            Der Artikel wird aus „{detail.name}“ gelöst und zählt nicht mehr zum Preisvergleich.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <label className="flex flex-col gap-1 text-[13px]">
          Artikel
          <select className="eingabe" value={artikelId} onChange={(e) => setArtikelId(e.target.value)}>
            {detail.artikel.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name} · {a.korrespondent.name}
              </option>
            ))}
          </select>
        </label>
        <AlertDialogFooter>
          <AlertDialogCancel>Abbrechen</AlertDialogCancel>
          <AlertDialogAction
            onClick={() =>
              artikel &&
              entfernen.mutateAsync({ id: detail.id, artikelId: artikel.id }).then(
                () => toast.erfolg(`„${artikel.name}“ herausgelöst`),
                (f: Error) => toast.fehler(f.message),
              )
            }
          >
            Herauslösen
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}

/** Hinweis beim einzelnen Artikel, zu welchem Stammartikel er gehört. */
function Zugehoerigkeit({ artikelId, name }: { artikelId: string; name: string }) {
  const { data } = useArtikel({ suche: name })
  const stamm = data?.eintraege.find((a) => a.id === artikelId)?.stammartikel
  if (!data) return null
  return (
    <section className="karte p-5 text-sm">
      {stamm ? (
        <>
          Gehört zu Stammartikel{' '}
          <Link to="/artikel/stammartikel/$stammartikelId" params={{ stammartikelId: stamm.id }}>
            {stamm.name}
          </Link>
        </>
      ) : (
        <>
          Keinem Stammartikel zugeordnet · <Link to="/artikel">Zur Artikelliste</Link>
        </>
      )}
    </section>
  )
}

/** Detailseite für Stammartikel und einzelne Artikel: Kopf, Kennzahlen, Preisverlauf, Käufe, Zuordnung. */
export function PreisDetailSeite({
  detail,
  istPending,
  jahr,
  preisart,
  onPreisart,
}: {
  detail: PreisDetail | undefined
  istPending: boolean
  jahr: number
  preisart: Preisart | undefined
  onPreisart: (preisart: Preisart) => void
}) {
  const { darfBearbeiten } = useRolle()
  if (istPending) return <main className="p-6" aria-busy="true" />
  if (!detail)
    return (
      <main className="p-6">
        <h1 className="m-0 text-[22px] font-semibold">Artikel nicht gefunden</h1>
        <p className="gedaempft">
          <Link to="/artikel">Zur Artikelliste</Link>
        </p>
      </main>
    )

  const mitGrundpreis = hatGrundpreis(detail)
  const art = mitGrundpreis ? (preisart ?? 'grund') : 'einzel'
  const korrespondenten = new Set(
    detail.artikel.length
      ? detail.artikel.map((a) => a.korrespondent.id)
      : detail.serien.map((s) => s.korrespondent.id),
  ).size
  const kopfzeile = [
    detail.basiseinheit && `Basiseinheit ${detail.basiseinheit}`,
    mitGrundpreis &&
      detail.basiseinheit &&
      `Grundpreis je ${formatZahl(detail.grundpreisMenge ?? 1)} ${detail.basiseinheit}`,
    detail.istStammartikel &&
      `${detail.artikel.length} Artikel von ${korrespondenten} ${korrespondenten === 1 ? 'Korrespondent' : 'Korrespondenten'}`,
    `${detail.kennzahlen.anzahlKaeufe} ${detail.kennzahlen.anzahlKaeufe === 1 ? 'Kauf' : 'Käufe'}`,
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <main className="p-6">
      <nav aria-label="Brotkrumen" className="gedaempft mb-2 text-[13px]">
        <Link to="/artikel">Artikel</Link>
        {detail.istStammartikel && <> / Stammartikel</>} / <span aria-current="page">{detail.name}</span>
      </nav>
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-3">
          <span className="flex size-10 flex-none items-center justify-center rounded-[9px] bg-[var(--color-akzent-zart)] text-[var(--color-akzent-text)]">
            <Icon name="inventory_2" />
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="m-0 text-[22px] font-semibold text-text">{detail.name}</h1>
              {detail.istStammartikel && <Chip ton="blau">Stammartikel</Chip>}
            </div>
            <p className="gedaempft m-0 mt-1 text-sm">{kopfzeile}</p>
          </div>
        </div>
        {detail.istStammartikel && darfBearbeiten && <Herausloesen detail={detail} />}
      </div>
      <Kennzahlen detail={detail} jahr={jahr} mitGrundpreis={mitGrundpreis} />
      <div className="flex flex-col gap-4">
        <Preisverlauf
          detail={detail}
          jahr={jahr}
          preisart={art}
          mitGrundpreis={mitGrundpreis}
          onPreisart={onPreisart}
        />
        <Kaeufe kaeufe={detail.kaeufe} />
        {detail.istStammartikel ? (
          <StammartikelKarte key={detail.id} detail={detail} darfBearbeiten={darfBearbeiten} />
        ) : (
          <Zugehoerigkeit artikelId={detail.id} name={detail.name} />
        )}
      </div>
    </main>
  )
}
