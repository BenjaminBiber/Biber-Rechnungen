import { Link, useLocation } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { belegDateiUrl, useBeleg } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegDetail } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { useRolle } from '@/components/shell/useRolle'
import { LadeBereich, Skelett, SkelettText } from '@/components/ui/skelett'
import { BestaetigenDialog } from '@/features/belege/detail/BestaetigenDialog'
import { DatenFormular } from '@/features/belege/detail/DatenFormular'
import { istZuPruefen, original } from '@/features/belege/detail/gemeinsam'
import { MehrMenue } from '@/features/belege/detail/MehrMenue'
import { TabLeiste } from '@/features/belege/detail/TabLeiste'
import { quelleText } from '@/features/belege/liste/suche'
import { VorschauDialog } from '@/features/belege/liste/Vorschau'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'

const kopfKnopf =
  'flex size-11 flex-none cursor-pointer items-center justify-center rounded-full border-0 bg-transparent text-text no-underline hover:text-text'

/** Mobile Beleg-Ansicht; `children` ist der Inhalt der Tabs (Outlet), der unter „Positionen, …“ aufklappt. */
export function MobilBeleg({ belegId, children }: { belegId: string; children: ReactNode }) {
  const { data: beleg, error, isPending } = useBeleg(belegId)
  const { darfBearbeiten } = useRolle()

  if (isPending)
    return (
      <LadeBereich als="main" label="Beleg wird geladen" className="flex flex-col gap-4 px-4 py-5">
        <Skelett hoehe={22} breite="60%" />
        <Skelett hoehe={12} breite="40%" />
        <Skelett hoehe={160} className="w-full rounded-lg" />
        <SkelettText zeilen={4} />
      </LadeBereich>
    )
  if (!beleg) {
    const nichtGefunden = istApiFehler(error) && error.status === 404
    return (
      <main className="px-4 py-5">
        <h1 className="m-0 text-[20px] font-semibold">
          {nichtGefunden ? 'Beleg nicht gefunden' : 'Beleg konnte nicht geladen werden'}
        </h1>
        <p className="gedaempft mt-2">
          <Link to="/belege">Zur Belegliste</Link>
        </p>
      </main>
    )
  }
  return (
    <Inhalt key={beleg.id} beleg={beleg} darfBearbeiten={darfBearbeiten}>
      {children}
    </Inhalt>
  )
}

function Inhalt({
  beleg,
  darfBearbeiten,
  children,
}: {
  beleg: BelegDetail
  darfBearbeiten: boolean
  children: ReactNode
}) {
  // Ein Tab (z. B. …/positionen, auch aus dem Mehr-Menü) klappt die Details auf.
  const pfad = useLocation({ select: (l) => l.pathname })
  const aufTab = !pfad.replace(/\/$/, '').endsWith(beleg.id)
  const [gewaehlt, setModus] = useState<'ansicht' | 'details' | 'bearbeiten'>('ansicht')
  const modus = aufTab && gewaehlt === 'ansicht' ? 'details' : gewaehlt
  const [vorschau, setVorschau] = useState(false)
  const datei = original(beleg)
  const originalUrl = datei ? belegDateiUrl(beleg.id, datei.id, true) : null
  const zuPruefen = istZuPruefen(beleg)
  const zeigeAktionen = darfBearbeiten && modus !== 'bearbeiten'

  return (
    <main className={`flex w-full min-w-0 flex-col text-text ${zeigeAktionen ? 'pb-[76px]' : 'pb-6'}`}>
      <div className="sticky top-0 z-10 flex items-center gap-1 border-b border-linie bg-flaeche px-1.5 py-1">
        <Link to="/belege" aria-label="Zurück" className={kopfKnopf}>
          <Icon name="arrow_back" />
        </Link>
        <span className="flex-1 text-[17px] font-semibold">Beleg</span>
        {originalUrl && (
          <a
            href={originalUrl}
            target="_blank"
            rel="noopener noreferrer"
            aria-label="Original öffnen"
            className={kopfKnopf}
          >
            <Icon name="open_in_new" />
          </a>
        )}
        <span className="[&_.btn]:size-11 [&_.btn]:justify-center [&_.btn]:border-0 [&_.btn]:bg-transparent">
          <MehrMenue beleg={beleg} darfBearbeiten={darfBearbeiten} kompakt />
        </span>
      </div>

      <div className="flex flex-col gap-3.5 px-4 pt-4">
        <div className="flex items-center gap-3">
          <KorrespondentLogo logo={beleg.logo} groesse={44} ton="orange" />
          <div className="min-w-0">
            <h1 className="m-0 truncate text-[17px] font-semibold">{beleg.korrespondent?.name ?? beleg.titel}</h1>
            {beleg.rechnungsnummer && (
              <div className="gedaempft text-sm">
                {beleg.belegart} <span className="mono text-text">{beleg.rechnungsnummer}</span>
              </div>
            )}
          </div>
        </div>

        {beleg.brutto != null && (
          <div className="num text-left text-[32px] font-semibold" aria-label="Betrag">
            {formatEuro(beleg.brutto, beleg.waehrung)}
          </div>
        )}

        <div className="flex flex-wrap gap-1.5">
          {beleg.tags.map((t) =>
            t.istSystem ? (
              <Chip key={t.id} ton="orange" icon="report">
                {t.name}
              </Chip>
            ) : (
              <Chip key={t.id} ton={t.farbe as ChipTon}>
                {t.name}
              </Chip>
            ),
          )}
          {beleg.kategorie && <Chip ton="grau">{beleg.kategorie.name}</Chip>}
        </div>

        <button
          type="button"
          onClick={() => setVorschau(true)}
          className="flex cursor-pointer flex-col items-center gap-2 rounded-karte border-0 bg-segment p-4"
        >
          {beleg.vorschauUrl ? (
            <img
              src={beleg.vorschauUrl}
              alt=""
              className="max-h-[220px] max-w-full bg-white object-contain shadow-blatt"
            />
          ) : (
            <span aria-hidden="true" className="h-[160px] w-[120px] bg-white shadow-blatt" />
          )}
          <span className="gedaempft text-[13px]">Tippen für Original</span>
        </button>

        <section className="karte p-4" aria-label="Eckdaten">
          <dl className="m-0 grid grid-cols-[120px_minmax(0,1fr)] gap-x-3 gap-y-2.5 text-sm [&_dd]:m-0 [&_dd]:text-right [&_dd]:tabular-nums [&_dt]:text-gedaempft">
            <dt>Rechnungsdatum</dt>
            <dd>{beleg.rechnungsdatum ? formatDatum(beleg.rechnungsdatum) : '–'}</dd>
            <dt>Fällig</dt>
            <dd className={beleg.ueberfaellig ? 'text-rot-text' : undefined}>
              {beleg.faelligkeitsdatum ? formatDatum(beleg.faelligkeitsdatum) : '–'}
              {beleg.ueberfaellig && ' · überfällig'}
            </dd>
            <dt>Netto</dt>
            <dd>{beleg.netto != null ? formatEuro(beleg.netto, beleg.waehrung) : '–'}</dd>
            {beleg.steuerzeilen.map((z) => (
              <Steuer key={z.satz} satz={z.satz} steuer={formatEuro(z.steuer, beleg.waehrung)} />
            ))}
            <dt>Quelle</dt>
            <dd>
              {quelleText[beleg.quelle]} · {beleg.format}
            </dd>
          </dl>
        </section>

        {modus !== 'bearbeiten' && (
          <button
            type="button"
            aria-expanded={modus === 'details'}
            onClick={() => setModus(modus === 'details' ? 'ansicht' : 'details')}
            className="flex min-h-11 cursor-pointer items-center justify-between rounded-karte border border-rand bg-karte px-4 text-left text-sm font-medium text-akzent-text"
          >
            Positionen, Prüfung, Historie und Dateien
            <Icon name={modus === 'details' ? 'expand_less' : 'expand_more'} />
          </button>
        )}
        {modus === 'details' && (
          <section className="karte min-w-0 overflow-hidden">
            <div className="overflow-x-auto">
              <TabLeiste beleg={beleg} />
            </div>
            <div className="flex min-w-0 flex-col gap-[18px] p-4">{children}</div>
          </section>
        )}
        {modus === 'bearbeiten' && (
          <section className="karte min-w-0 p-4" aria-label="Beleg bearbeiten">
            <DatenFormular beleg={beleg} darfBearbeiten={darfBearbeiten} />
            <button
              type="button"
              className="btn mt-3 min-h-11 w-full justify-center"
              onClick={() => setModus('ansicht')}
            >
              Schließen
            </button>
          </section>
        )}
      </div>

      {zeigeAktionen && (
        <div className="fixed inset-x-0 bottom-[84px] z-20 flex gap-2.5 border-t border-linie bg-karte px-4 py-2.5 [&_.btn]:h-11 [&_.btn]:flex-1 [&_.btn]:justify-center [&>*]:flex [&>*]:flex-1">
          <button type="button" className="btn" onClick={() => setModus('bearbeiten')}>
            <Icon name="edit" />
            Bearbeiten
          </button>
          {zuPruefen && (
            <span>
              <BestaetigenDialog beleg={beleg} />
            </span>
          )}
        </div>
      )}

      <VorschauDialog beleg={vorschau ? beleg : null} onSchliessen={() => setVorschau(false)} />
    </main>
  )
}

function Steuer({ satz, steuer }: { satz: number; steuer: string }) {
  return (
    <>
      <dt>USt {formatZahl(satz)} %</dt>
      <dd>{steuer}</dd>
    </>
  )
}
