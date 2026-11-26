import { Link } from '@tanstack/react-router'
import type { Dashboard } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { formatEuro, formatProzent } from '@/lib/format'
import { type DashboardZeitraum, monatsNamenLang } from './gemeinsam'

const zwei = (n: number) => String(n).padStart(2, '0')

/** Bezugsmonat (0–11): im laufenden Jahr der aktuelle Monat, sonst Dezember. */
function bezugsMonat(jahr: number, heute: Date): number {
  return jahr === heute.getFullYear() ? heute.getMonth() : 11
}

/** Bezeichnung und Datumsgrenzen des gewählten Zeitraums. */
export function zeitraumGrenzen(
  zeitraum: DashboardZeitraum,
  jahr: number,
  heute: Date,
): { text: string; von: string; bis: string } {
  const monat = bezugsMonat(jahr, heute)
  const letzterTag = (m: number) => new Date(Date.UTC(jahr, m + 1, 0)).getUTCDate()
  if (zeitraum === 'Quartal') {
    const start = monat - (monat % 3)
    return {
      text: `Q${start / 3 + 1}`,
      von: `${jahr}-${zwei(start + 1)}-01`,
      bis: `${jahr}-${zwei(start + 3)}-${zwei(letzterTag(start + 2))}`,
    }
  }
  return {
    text: monatsNamenLang[monat],
    von: `${jahr}-${zwei(monat + 1)}-01`,
    bis: `${jahr}-${zwei(monat + 1)}-${zwei(letzterTag(monat))}`,
  }
}

type Karte = {
  icon: string
  iconFarbe: string
  titel: string
  wert: string
  unterzeile: string
  unterFarbe?: string
  link: { to: '/belege' | '/export'; search?: Record<string, string> }
}

function vergleich(prozent: number | null | undefined, gegen: string | null | undefined): string {
  if (prozent == null || !gegen) return ''
  return `${formatProzent(prozent / 100, true)} ggü. ${gegen}`
}

/** Sechs Kennzahl-Karten; jede ist ein Link auf die passende Liste. */
export function Kennzahlen({
  daten,
  zeitraum,
  jahr,
  heute,
}: {
  daten: Dashboard
  zeitraum: DashboardZeitraum
  jahr: number
  heute: Date
}) {
  const grenzen = zeitraumGrenzen(zeitraum, jahr, heute)
  const { kosten, kostenJahr, offen, ueberfaellig, inbox, zuPruefen } = daten
  const ueberfaelligZusatz =
    ueberfaellig.aeltesteTage != null ? ` · älteste seit ${ueberfaellig.aeltesteTage} Tagen` : ''
  const karten: Karte[] = [
    {
      icon: 'payments',
      iconFarbe: 'var(--color-akzent-text)',
      titel: `Kosten ${grenzen.text}`,
      wert: formatEuro(kosten.betrag ?? 0),
      unterzeile: vergleich(kosten.aenderungProzent, kosten.vergleichszeitraum),
      link: { to: '/belege', search: { von: grenzen.von, bis: grenzen.bis } },
    },
    {
      icon: 'calendar_today',
      iconFarbe: 'var(--color-akzent-text)',
      titel: `Kosten ${jahr}`,
      wert: formatEuro(kostenJahr.betrag ?? 0),
      unterzeile: vergleich(kostenJahr.aenderungProzent, kostenJahr.vergleichszeitraum),
      link: { to: '/export' },
    },
    {
      icon: 'schedule',
      iconFarbe: 'var(--color-gedaempft-stark)',
      titel: 'Offen',
      wert: String(offen.anzahl),
      unterzeile: `${formatEuro(offen.betrag ?? 0)} ohne Zahlungs-Tag`,
      link: { to: '/belege', search: { ansicht: 'Offen' } },
    },
    {
      icon: 'event_busy',
      iconFarbe: 'var(--color-rot-text)',
      titel: 'Überfällig',
      wert: String(ueberfaellig.anzahl),
      unterzeile: `${formatEuro(ueberfaellig.betrag ?? 0)}${ueberfaelligZusatz}`,
      unterFarbe: 'var(--color-rot-text)',
      link: { to: '/belege', search: { ansicht: 'Ueberfaellig' } },
    },
    {
      icon: 'inbox',
      iconFarbe: 'var(--color-akzent-text)',
      titel: 'Inbox',
      wert: String(inbox.anzahl),
      unterzeile: 'neu, noch nicht gesichtet',
      link: { to: '/belege', search: { ansicht: 'Inbox' } },
    },
    {
      icon: 'report',
      iconFarbe: 'var(--color-orange-text)',
      titel: 'Zu prüfen',
      wert: String(zuPruefen.anzahl),
      unterzeile: 'Prüfung fehlgeschlagen',
      unterFarbe: 'var(--color-orange-text)',
      link: { to: '/belege', search: { ansicht: 'ZuPruefen' } },
    },
  ]

  return (
    <div className="grid grid-cols-2 gap-3.5 sm:grid-cols-3 2xl:grid-cols-6">
      {karten.map((k) => (
        <Link
          key={k.titel}
          to={k.link.to}
          search={k.link.search as never}
          className="karte flex flex-col gap-1.5 p-4 text-text no-underline hover:text-text"
        >
          <span className="flex items-center gap-1.5 text-[13px] text-[var(--color-gedaempft-stark)]">
            <span style={{ color: k.iconFarbe }}>
              <Icon name={k.icon} className="text-[18px]" />
            </span>
            <span>{k.titel}</span>
          </span>
          <span className="num text-left text-2xl font-semibold">{k.wert}</span>
          <span className="text-xs" style={{ color: k.unterFarbe ?? 'var(--color-gedaempft-stark)' }}>
            {k.unterzeile}
          </span>
        </Link>
      ))}
    </div>
  )
}
