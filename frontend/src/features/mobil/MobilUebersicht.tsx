import { Link } from '@tanstack/react-router'
import { useDashboard } from '@/api/dashboard'
import type { Dashboard } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { LadeBereich, Skelett, SkelettKarte, SkelettListe } from '@/components/ui/skelett'
import { monatsIndex, monatsNamenLang } from '@/features/dashboard/gemeinsam'
import { formatEuro, formatProzent } from '@/lib/format'

const kachel = 'karte flex min-h-[88px] min-w-0 flex-col gap-1 p-3.5 text-text no-underline hover:text-text'

function belegeText(anzahl: number): string {
  return anzahl === 1 ? '1 Beleg' : `${anzahl} Belege`
}

/** Mobile Übersicht: vier Kacheln, Kosten pro Monat als kleines Balkendiagramm und die ersten drei „Zu prüfen“. */
export function MobilUebersicht({ jahr }: { jahr: number }) {
  const dashboard = useDashboard({ zeitraum: 'Jahr', jahr })

  if (dashboard.isPending)
    return (
      <LadeBereich
        als="main"
        label="Übersicht wird geladen"
        className="flex w-full min-w-0 flex-col gap-3.5 px-4 pt-4 pb-6"
      >
        <div className="grid grid-cols-2 gap-2.5">
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className="karte flex min-h-[88px] flex-col justify-between gap-1 p-3.5">
              <Skelett hoehe={10} breite="55%" />
              <Skelett hoehe={22} breite="70%" />
            </div>
          ))}
        </div>
        <SkelettKarte hoehe={96} />
        <SkelettListe zeilen={3} label={null} />
      </LadeBereich>
    )
  if (!dashboard.data)
    return (
      <main className="px-4 py-5">
        <div role="alert" className="karte flex flex-col items-start gap-3 p-4">
          <p className="m-0 text-sm text-rot-text">Die Übersicht konnte nicht geladen werden.</p>
          <button type="button" className="btn" onClick={() => void dashboard.refetch()}>
            <Icon name="refresh" />
            Erneut versuchen
          </button>
        </div>
      </main>
    )
  return <Inhalt daten={dashboard.data} jahr={jahr} />
}

function Inhalt({ daten, jahr }: { daten: Dashboard; jahr: number }) {
  const { inbox, zuPruefen, offen, ueberfaellig, kostenJahr } = daten
  return (
    <main className="flex w-full min-w-0 flex-col gap-3.5 px-4 pt-4 pb-6 text-text">
      <h1 className="sr-only">Dashboard</h1>
      <div className="grid grid-cols-2 gap-2.5">
        <Link to="/belege" search={{ ansicht: 'Inbox' } as never} className={kachel}>
          <span className="flex items-center gap-1.5 text-[13px] text-akzent-text">
            <Icon name="inbox" className="text-[18px]" />
            Inbox
          </span>
          <span className="num text-left text-2xl font-semibold">{inbox.anzahl}</span>
        </Link>
        <Link to="/belege" search={{ ansicht: 'ZuPruefen' } as never} className={kachel}>
          <span className="flex items-center gap-1.5 text-[13px] text-orange-text">
            <Icon name="report" className="text-[18px]" />
            Zu prüfen
          </span>
          <span className="num text-left text-2xl font-semibold">{zuPruefen.anzahl}</span>
        </Link>
        <div className={kachel}>
          <span className="gedaempft text-[13px]">Offen</span>
          <span className="num text-left text-lg font-semibold">{formatEuro(offen.betrag ?? 0)}</span>
          <span className="gedaempft text-xs">{belegeText(offen.anzahl)}</span>
        </div>
        <div className={`${kachel} border-rot-text bg-rot-flaeche`}>
          <span className="text-[13px] text-rot-text">Überfällig</span>
          <span className="num text-left text-lg font-semibold text-rot-text">
            {formatEuro(ueberfaellig.betrag ?? 0)}
          </span>
          <span className="text-xs text-rot-text">{belegeText(ueberfaellig.anzahl)}</span>
        </div>
      </div>

      <section className="karte flex min-w-0 flex-col gap-1 p-4" aria-labelledby="mobil-kosten">
        <div className="flex items-baseline justify-between gap-2">
          <h2 id="mobil-kosten" className="m-0 text-[15px] font-semibold">
            Gesamtkosten pro Monat
          </h2>
          <span className="gedaempft text-xs">{jahr} · brutto</span>
        </div>
        <span className="num text-left text-[22px] font-semibold">{formatEuro(kostenJahr.betrag ?? 0)}</span>
        {kostenJahr.aenderungProzent != null && kostenJahr.vergleichszeitraum && (
          <span className="gedaempft text-xs">
            {formatProzent(kostenJahr.aenderungProzent / 100, true)} ggü. {kostenJahr.vergleichszeitraum}
          </span>
        )}
        <Balken monate={daten.kostenProMonat.filter((m) => m.summe > 0)} />
      </section>

      <section className="karte flex min-w-0 flex-col p-4" aria-labelledby="mobil-pruefen">
        <div className="flex items-center justify-between">
          <h2 id="mobil-pruefen" className="m-0 text-[15px] font-semibold">
            Zu prüfen
          </h2>
          <Link
            to="/belege"
            search={{ ansicht: 'ZuPruefen' } as never}
            className="flex min-h-11 items-center px-2 text-sm"
            aria-label="Alle zu prüfenden Belege"
          >
            Alle
          </Link>
        </div>
        {daten.zuPruefenListe.length === 0 && <p className="gedaempft m-0 text-sm">Nichts zu prüfen.</p>}
        {daten.zuPruefenListe.slice(0, 3).map((e) => (
          <Link
            key={e.belegId}
            to="/belege/$belegId"
            params={{ belegId: e.belegId }}
            className="flex min-h-11 items-center gap-2.5 border-t border-linie py-2.5 text-text no-underline hover:text-text"
          >
            <span className="text-orange-text">
              <Icon name="report" />
            </span>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate text-sm font-medium">{e.korrespondent ?? 'Unbekannter Korrespondent'}</span>
              <span className="truncate text-xs text-orange-text">{e.grund}</span>
            </span>
            {e.brutto != null && <span className="num text-sm font-medium">{formatEuro(e.brutto)}</span>}
          </Link>
        ))}
      </section>
    </main>
  )
}

function Balken({ monate }: { monate: Dashboard['kostenProMonat'] }) {
  const max = Math.max(1, ...monate.map((m) => m.summe))
  return (
    <figure className="m-0 mt-2" aria-label="Kosten je Monat">
      <div className="flex h-[90px] items-end gap-1">
        {monate.map((m) => (
          <div
            key={m.monat}
            title={`${monatsNamenLang[monatsIndex(m.monat)]}: ${formatEuro(m.summe)}`}
            className="flex-1 rounded-t-[3px] bg-akzent"
            style={{ height: `${Math.max(2, Math.round((m.summe / max) * 90))}px` }}
          />
        ))}
      </div>
      <div className="mt-1 flex gap-1" aria-hidden="true">
        {monate.map((m) => (
          <span key={m.monat} className="gedaempft flex-1 text-center text-[11px]">
            {monatsNamenLang[monatsIndex(m.monat)][0]}
          </span>
        ))}
      </div>
    </figure>
  )
}
