import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useArtikel, useStammartikel } from '@/api/artikel'
import { useKorrespondenten } from '@/api/korrespondenten'
import type { ArtikelListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { LadeBereich, Skelett, SkelettTabelle } from '@/components/ui/skelett'
import { ArtikelFilter } from '@/features/artikel/ArtikelFilter'
import { ArtikelTabelle } from '@/features/artikel/ArtikelTabelle'
import { StammartikelDialog } from '@/features/artikel/StammartikelDialog'
import { StammartikelKarten } from '@/features/artikel/StammartikelKarten'
import { ohneLeere, stammartikelFiltern, sucheValidieren, zuAbfrage, type ArtikelSuche } from '@/features/artikel/suche'
import { gruppe, gruppeGrau } from '@/features/belege/liste/bausteine'
import { cn } from '@/lib/utils'

/** Artikel mit den Reitern „Artikel“ und „Stammartikel“; Filter und Reiter stehen in der URL. */
export const Route = createFileRoute('/_app/artikel/')({
  validateSearch: (roh: Record<string, unknown>): ArtikelSuche => sucheValidieren(roh),
  component: ArtikelSeite,
})

function ArtikelSeite() {
  const suche = Route.useSearch()
  const navigate = Route.useNavigate()
  const { darfBearbeiten } = useRolle()
  const [gewaehlt, setGewaehlt] = useState<ArtikelListeneintrag[]>([])
  const [dialogOffen, setDialogOffen] = useState(false)

  const reiter = suche.reiter ?? 'artikel'
  const liste = useArtikel(zuAbfrage(suche))
  const alleArtikel = useArtikel({ seitengroesse: 1 })
  const stammartikel = useStammartikel()
  const korrespondenten = useKorrespondenten()
  const korrespondentName = korrespondenten.data?.find((k) => k.id === suche.korrespondentId)?.name
  const stammartikelTreffer = stammartikelFiltern(stammartikel.data ?? [], suche, korrespondentName)

  function setze(teil: Partial<ArtikelSuche>) {
    void navigate({
      search: (alt: ArtikelSuche) => ohneLeere({ ...alt, ...('seite' in teil ? {} : { seite: undefined }), ...teil }),
    })
  }

  function umschalten(a: ArtikelListeneintrag) {
    setGewaehlt((g) => (g.some((x) => x.id === a.id) ? g.filter((x) => x.id !== a.id) : [...g, a]))
  }

  const anzahlArtikel = alleArtikel.data?.gesamt
  const anzahlStamm = stammartikel.data?.length

  return (
    <main className="seite flex flex-col gap-3.5 text-text">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex min-w-0 flex-wrap items-center gap-x-5 gap-y-3">
          <h1 className="m-0 text-[22px] font-semibold">Artikel</h1>
          <div role="tablist" aria-label="Ansicht" className={gruppe}>
            {(
              [
                ['artikel', 'Artikel', anzahlArtikel],
                ['stammartikel', 'Stammartikel', anzahlStamm],
              ] as const
            ).map(([wert, text, anzahl]) => (
              <button
                key={wert}
                type="button"
                role="tab"
                aria-selected={reiter === wert}
                onClick={() => setze({ reiter: wert === 'artikel' ? undefined : wert })}
                className={cn(reiter === wert && '!bg-akzent !text-auf-akzent')}
              >
                {text}
                {anzahl !== undefined && ` (${anzahl})`}
              </button>
            ))}
          </div>
        </div>
        {darfBearbeiten && (
          <button
            type="button"
            className="btn btn-primaer"
            disabled={gewaehlt.length < 2}
            onClick={() => setDialogOffen(true)}
          >
            <Icon name="merge" />
            {gewaehlt.length < 2
              ? 'Zu Stammartikel zusammenführen'
              : `${gewaehlt.length} zu Stammartikel zusammenführen`}
          </button>
        )}
      </div>

      <ArtikelFilter
        suche={suche}
        setze={setze}
        reiter={reiter}
        ladend={reiter === 'artikel' ? liste.isFetching : stammartikel.isFetching}
      />

      {reiter === 'artikel' ? (
        <>
          {liste.isPending ? (
            <SkelettTabelle zeilen={8} spalten={darfBearbeiten ? 9 : 8} label="Artikel werden geladen" />
          ) : liste.isError && !liste.data ? (
            <p className="karte m-0 p-6 text-center">Die Artikel konnten nicht geladen werden.</p>
          ) : liste.data.eintraege.length === 0 ? (
            <p className="karte m-0 p-6 text-center">Keine Artikel für diese Filter</p>
          ) : (
            <>
              <div
                className={cn(liste.isPlaceholderData && 'veraltet')}
                aria-busy={(liste.isPlaceholderData && liste.isFetching) || undefined}
              >
                <ArtikelTabelle
                  eintraege={liste.data.eintraege}
                  gewaehlt={darfBearbeiten ? gewaehlt.map((a) => a.id) : undefined}
                  onUmschalten={darfBearbeiten ? umschalten : undefined}
                />
              </div>
              <Blaettern
                seite={liste.data.seite}
                seiten={Math.max(1, Math.ceil(liste.data.gesamt / liste.data.seitengroesse))}
                gesamt={liste.data.gesamt}
                onSeite={(s) => setze({ seite: s > 1 ? s : undefined })}
              />
            </>
          )}
        </>
      ) : stammartikel.isPending ? (
        <StammartikelSkelett />
      ) : (stammartikel.data?.length ?? 0) > 0 && stammartikelTreffer.length === 0 ? (
        <p className="karte m-0 p-6 text-center">Keine Stammartikel für diese Filter</p>
      ) : (
        <StammartikelKarten eintraege={stammartikelTreffer} />
      )}

      {darfBearbeiten && (
        <StammartikelDialog
          artikel={gewaehlt}
          offen={dialogOffen}
          onSchliessen={() => {
            setDialogOffen(false)
          }}
        />
      )}
    </main>
  )
}

function Blaettern({
  seite,
  seiten,
  gesamt,
  onSeite,
}: {
  seite: number
  seiten: number
  gesamt: number
  onSeite: (seite: number) => void
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-2.5 text-sm">
      <span>
        {gesamt === 1 ? '1 Artikel' : `${gesamt} Artikel`} · Seite {seite} von {seiten}
      </span>
      {seiten > 1 && (
        <nav aria-label="Seiten" className={gruppeGrau}>
          <button type="button" aria-label="Vorherige Seite" disabled={seite <= 1} onClick={() => onSeite(seite - 1)}>
            <Icon name="chevron_left" />
          </button>
          <button
            type="button"
            aria-label="Nächste Seite"
            disabled={seite >= seiten}
            onClick={() => onSeite(seite + 1)}
          >
            <Icon name="chevron_right" />
          </button>
        </nav>
      )}
    </div>
  )
}

/** Kartenraster wie in `StammartikelKarten`. */
function StammartikelSkelett() {
  return (
    <LadeBereich
      als="div"
      label="Stammartikel werden geladen"
      className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(260px,1fr))] gap-3.5 p-0"
    >
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="karte flex flex-col gap-2.5 p-4">
          <Skelett hoehe={14} breite={`${60 + ((i * 11) % 30)}%`} />
          <Skelett hoehe={10} breite="40%" />
          <Skelett hoehe={32} className="w-full rounded" />
          <Skelett hoehe={12} breite="55%" />
        </div>
      ))}
    </LadeBereich>
  )
}
