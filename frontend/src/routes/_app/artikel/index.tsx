import { createFileRoute } from '@tanstack/react-router'
import { useState } from 'react'
import { useArtikel, useStammartikel } from '@/api/artikel'
import { useKorrespondenten } from '@/api/korrespondenten'
import type { ArtikelListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
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

      <ArtikelFilter suche={suche} setze={setze} reiter={reiter} />

      {reiter === 'artikel' ? (
        <>
          {liste.isPending ? (
            <Laden />
          ) : liste.isError && !liste.data ? (
            <p className="karte m-0 p-6 text-center">Die Artikel konnten nicht geladen werden.</p>
          ) : liste.data.eintraege.length === 0 ? (
            <p className="karte m-0 p-6 text-center">Keine Artikel für diese Filter</p>
          ) : (
            <>
              <ArtikelTabelle
                eintraege={liste.data.eintraege}
                gewaehlt={darfBearbeiten ? gewaehlt.map((a) => a.id) : undefined}
                onUmschalten={darfBearbeiten ? umschalten : undefined}
              />
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
        <Laden />
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

function Laden() {
  return (
    <div className="karte flex flex-col gap-3 p-4" role="status" aria-label="Wird geladen">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="h-6 animate-pulse rounded bg-segment" />
      ))}
    </div>
  )
}
