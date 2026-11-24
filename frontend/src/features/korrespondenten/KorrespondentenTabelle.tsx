import { Link } from '@tanstack/react-router'
import type { KorrespondentListeneintrag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { Checkbox } from '@/components/ui/checkbox'
import { formatDatum, formatEuro } from '@/lib/format'
import { matchingKurztext } from '@/lib/matching'
import { KorrespondentLogo, type KorrespondentenTyp } from './gemeinsam'

function Steuerkennung({ k }: { k: KorrespondentListeneintrag }) {
  if (k.ustIdNr) return <span className="mono">{k.ustIdNr}</span>
  if (k.steuernummer) return <span className="mono">Steuernr. {k.steuernummer}</span>
  return <span className="gedaempft">—</span>
}

function Leerzustand({ typ }: { typ: KorrespondentenTyp }) {
  if (typ === 'Kunden')
    return (
      <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
        <Icon name="person_off" className="text-[32px] text-gedaempft" />
        <p className="m-0 font-semibold">Noch keine Kunden</p>
        <p className="gedaempft m-0 text-sm">Kunden kommen mit den Ausgangsrechnungen in einer späteren Ausbaustufe.</p>
      </div>
    )
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <Icon name="search_off" className="text-[32px] text-gedaempft" />
      <p className="m-0 font-semibold">Keine Treffer</p>
    </div>
  )
}

/** Korrespondentenliste mit optionaler Auswahlspalte (nur mit Bearbeiten-Recht). */
export function KorrespondentenTabelle({
  eintraege,
  typ,
  jahr,
  auswahl,
  onAuswahl,
}: {
  eintraege: KorrespondentListeneintrag[]
  typ: KorrespondentenTyp
  jahr: number
  /** Ohne `onAuswahl` entfällt die Auswahlspalte. */
  auswahl: string[]
  onAuswahl?: (ids: string[]) => void
}) {
  if (eintraege.length === 0) return <Leerzustand typ={typ} />
  const alleGewaehlt = eintraege.every((k) => auswahl.includes(k.id))
  const umschalten = (id: string) =>
    onAuswahl?.(auswahl.includes(id) ? auswahl.filter((x) => x !== id) : [...auswahl, id])
  return (
    <div className="overflow-x-auto">
      <table className="tabelle">
        <thead>
          <tr>
            {onAuswahl && (
              <th className="w-10">
                <Checkbox
                  aria-label="Alle auswählen"
                  checked={alleGewaehlt ? true : auswahl.length ? 'indeterminate' : false}
                  onCheckedChange={() => onAuswahl(alleGewaehlt ? [] : eintraege.map((k) => k.id))}
                />
              </th>
            )}
            <th>Korrespondent</th>
            <th>USt-IdNr.</th>
            <th>Default-Kategorie</th>
            <th>Matching</th>
            <th className="text-right">Belege</th>
            <th className="text-right">Kosten {jahr}</th>
            <th>Letzter Beleg</th>
          </tr>
        </thead>
        <tbody>
          {eintraege.map((k) => {
            const gewaehlt = auswahl.includes(k.id)
            return (
              <tr
                key={k.id}
                aria-selected={onAuswahl ? gewaehlt : undefined}
                className={gewaehlt ? 'bg-akzent-zart' : ''}
              >
                {onAuswahl && (
                  <td>
                    <Checkbox
                      aria-label={`${k.name} auswählen`}
                      checked={gewaehlt}
                      onCheckedChange={() => umschalten(k.id)}
                    />
                  </td>
                )}
                <td>
                  <div className="flex items-center gap-2.5">
                    <KorrespondentLogo logo={k.logo} />
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <Link
                          to="/korrespondenten/$korrespondentId"
                          params={{ korrespondentId: k.id }}
                          className="font-medium text-text no-underline hover:underline"
                        >
                          {k.name}
                        </Link>
                        {k.istNeu && <Chip ton="blau">neu</Chip>}
                      </div>
                      {k.hinweis && <div className="gedaempft text-xs">{k.hinweis}</div>}
                    </div>
                  </div>
                </td>
                <td>
                  <Steuerkennung k={k} />
                </td>
                <td>{k.defaultKategorie?.name ?? <span className="gedaempft">—</span>}</td>
                <td>{matchingKurztext(k.matching)}</td>
                <td className="num text-right">{k.anzahlBelege}</td>
                <td className="num text-right">{formatEuro(k.kostenJahr)}</td>
                <td>{k.letzterBeleg ? formatDatum(k.letzterBeleg) : <span className="gedaempft">—</span>}</td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
