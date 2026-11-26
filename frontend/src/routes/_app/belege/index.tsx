import { createFileRoute } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { useBelege } from '@/api/belege'
import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { LadeBereich, Skelett, SkelettTabelle, SkelettText } from '@/components/ui/skelett'
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
  type Darstellung,
  type ListenZustand,
} from '@/features/belege/liste/suche'
import { VorschauPanel } from '@/features/belege/liste/Vorschau'
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
  const { data: liste, isPending, isError, refetch, isFetching, isPlaceholderData } = useBelege(abfrage)
  const [spalten, setSpalten] = useSpalten()
  const [vorschau, setVorschau] = useState<BelegListeneintrag | null>(null)
  const auswahl = useBelegAuswahl()
  const darstellung = suche.darstellung ?? 'tabelle'
  const eintraege = liste?.eintraege ?? []

  let inhalt
  if (isPending) inhalt = <ListenSkelett darstellung={darstellung} spalten={spalten.length} />
  else if (isError && !liste)
    inhalt = (
      <Hinweis icon="error" text="Die Belege konnten nicht geladen werden.">
        <button
          type="button"
          className="btn"
          onClick={() => void refetch()}
          disabled={isFetching}
          aria-busy={isFetching || undefined}
        >
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

  // Filterleiste in allen Darstellungen; bei Auswahl ersetzt sie die Massenaktionen-Leiste.
  const leiste = auswahl.ids.length > 0 ? <Massenaktionen /> : <Filterleiste zustand={zustand} />

  return (
    <main className="seite flex flex-col gap-3.5 text-text">
      <Kopfzeile zustand={zustand} spalten={spalten} onSpaltenAendern={setSpalten} />
      {leiste}
      {liste && liste.gesamt > 0 && (
        <Zusammenfassung liste={liste} onSeite={(seite) => setze({ seite: seite > 1 ? seite : undefined })} />
      )}
      <div className="veraltet min-w-0" aria-busy={(isPlaceholderData && isFetching) || undefined}>
        {inhalt}
      </div>
      <VorschauPanel beleg={vorschau} onSchliessen={() => setVorschau(null)} />
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

function ListenSkelett({ darstellung, spalten }: { darstellung: Darstellung; spalten: number }) {
  if (darstellung === 'klein')
    return (
      <LadeBereich
        label="Belege werden geladen"
        className="grid grid-cols-[repeat(auto-fill,minmax(176px,1fr))] gap-3.5"
      >
        {Array.from({ length: 12 }, (_, i) => (
          <div key={i} className="karte flex flex-col overflow-hidden">
            <Skelett hoehe={196} className="rounded-none" />
            <div className="flex flex-col gap-2.5 border-t border-rand px-3 py-3">
              <Skelett hoehe={12} breite="85%" />
              <Skelett hoehe={10} breite="55%" />
              <Skelett hoehe={10} breite="40%" />
              <Skelett hoehe={10} breite="48%" />
            </div>
          </div>
        ))}
      </LadeBereich>
    )
  if (darstellung === 'gross')
    return (
      <LadeBereich label="Belege werden geladen" className="flex flex-col gap-3.5">
        {Array.from({ length: 4 }, (_, i) => (
          <div key={i} className="karte flex flex-wrap overflow-hidden">
            <Skelett hoehe={196} className="flex-[0_0_248px] rounded-none" />
            <div className="flex min-w-0 flex-[1_1_420px] flex-col gap-3 px-5 py-4">
              <Skelett hoehe={18} breite="50%" />
              <SkelettText zeilen={3} />
              <span className="flex-1" />
              <div className="flex flex-wrap justify-between gap-3">
                <Skelett hoehe={30} breite={280} className="rounded-[var(--radius-button)]" />
                <Skelett hoehe={12} breite={220} className="self-center" />
              </div>
            </div>
          </div>
        ))}
      </LadeBereich>
    )
  // So viele Spalten wie eingeblendet, damit die Tabelle beim Erscheinen nicht umbricht.
  return <SkelettTabelle zeilen={10} spalten={Math.min(Math.max(spalten, 3), 8)} label="Belege werden geladen" />
}
