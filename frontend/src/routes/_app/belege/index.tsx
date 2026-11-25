import { createFileRoute } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { useBelege } from '@/api/belege'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { BelegAuswahlProvider, useBelegAuswahl } from '@/features/belege/liste/auswahl'
import { BelegTabelle } from '@/features/belege/liste/BelegTabelle'
import { Filterleiste } from '@/features/belege/liste/Filterleiste'
import { GrosseKarten } from '@/features/belege/liste/GrosseKarten'
import { KleineKarten } from '@/features/belege/liste/KleineKarten'
import { Kopfzeile } from '@/features/belege/liste/Kopfzeile'
import { Massenaktionen } from '@/features/belege/liste/Massenaktionen'
import { useSpalten } from '@/features/belege/liste/spaltenDefinitionen'
import {
  filterZurueckgesetzt,
  ohneLeere,
  sucheValidieren,
  zuAbfrage,
  type BelegeSuche,
  type ListenZustand,
} from '@/features/belege/liste/suche'
import { VorschauDialog } from '@/features/belege/liste/VorschauDialog'
import { Zusammenfassung } from '@/features/belege/liste/Zusammenfassung'
import { MobilBelege } from '@/features/mobil/MobilBelege'
import { useIstMobil } from '@/lib/useIstMobil'

/** Belegliste; der gesamte Listen-Zustand steht in den Suchparametern (teilbar, Zurück-Taste). */
export const Route = createFileRoute('/_app/belege/')({
  validateSearch: (roh: Record<string, unknown>): BelegeSuche => sucheValidieren(roh),
  component: BelegeSeite,
})

function BelegeSeite() {
  const suche = Route.useSearch()
  const navigate = Route.useNavigate()
  const istMobil = useIstMobil()
  const zustand: ListenZustand = {
    suche,
    setze: (teil) =>
      void navigate({
        search: (alt: BelegeSuche) => ohneLeere({ ...alt, ...('seite' in teil ? {} : { seite: undefined }), ...teil }),
      }),
    ersetze: (neu) => void navigate({ search: ohneLeere(neu) }),
  }
  if (istMobil) return <MobilBelege zustand={zustand} />
  // Die Auswahl gilt für Filter, Sortierung und Seite – nicht für die Darstellung.
  return (
    <BelegAuswahlProvider schluessel={JSON.stringify({ ...suche, darstellung: undefined })}>
      <BelegeListe zustand={zustand} />
    </BelegAuswahlProvider>
  )
}

function BelegeListe({ zustand }: { zustand: ListenZustand }) {
  const { suche, setze, ersetze } = zustand
  const abfrage = zuAbfrage(suche)
  const { data: liste, isPending, isError, refetch, isFetching } = useBelege(abfrage)
  const [spalten, setSpalten] = useSpalten()
  const [vorschau, setVorschau] = useState<BelegListeneintrag | null>(null)
  const auswahl = useBelegAuswahl()
  const darstellung = suche.darstellung ?? 'tabelle'
  const eintraege = liste?.eintraege ?? []

  let inhalt
  if (isPending) inhalt = <Skelett />
  else if (isError && !liste)
    inhalt = (
      <Hinweis icon="error" text="Die Belege konnten nicht geladen werden.">
        <button type="button" className="btn" onClick={() => void refetch()} disabled={isFetching}>
          <Icon name="refresh" />
          Erneut versuchen
        </button>
      </Hinweis>
    )
  else if (eintraege.length === 0)
    inhalt = (
      <Hinweis icon="search_off" text="Keine Belege für diese Filter">
        <button type="button" className="btn" onClick={() => ersetze(filterZurueckgesetzt(suche))}>
          <Icon name="close" />
          Filter zurücksetzen
        </button>
      </Hinweis>
    )
  else if (darstellung === 'klein') inhalt = <KleineKarten eintraege={eintraege} onVorschau={setVorschau} />
  else if (darstellung === 'gross') inhalt = <GrosseKarten eintraege={eintraege} onVorschau={setVorschau} />
  else inhalt = <BelegTabelle eintraege={eintraege} spalten={spalten} zustand={zustand} />

  return (
    <main className="seite flex flex-col gap-3.5 text-text">
      <Kopfzeile
        zustand={zustand}
        abfrage={abfrage}
        seitenIds={eintraege.map((b) => b.id)}
        gesamt={liste?.gesamt ?? 0}
        spalten={spalten}
        onSpaltenAendern={setSpalten}
      />
      {auswahl.ids.length > 0 ? <Massenaktionen /> : <Filterleiste zustand={zustand} />}
      {liste && liste.gesamt > 0 && (
        <Zusammenfassung liste={liste} onSeite={(seite) => setze({ seite: seite > 1 ? seite : undefined })} />
      )}
      {inhalt}
      <VorschauDialog beleg={vorschau} onSchliessen={() => setVorschau(null)} />
    </main>
  )
}

function Hinweis({ icon, text, children }: { icon: string; text: string; children: ReactNode }) {
  return (
    <div className="karte flex flex-col items-center gap-3 px-6 py-12 text-center">
      <Icon name={icon} className="text-[32px] text-gedaempft" />
      <p className="m-0 text-[15px] font-medium">{text}</p>
      {children}
    </div>
  )
}

function Skelett() {
  return (
    <div className="karte flex flex-col gap-3 p-4" role="status" aria-label="Belege werden geladen">
      {Array.from({ length: 8 }, (_, i) => (
        <div key={i} className="h-6 animate-pulse rounded bg-segment" />
      ))}
    </div>
  )
}
