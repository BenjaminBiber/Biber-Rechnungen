import { Link } from '@tanstack/react-router'
import { useBelegWiederherstellen, useGeloeschteBelege } from '@/api/aufbewahrung'
import { belegDateiUrl } from '@/api/belege'
import type { GeloeschterBeleg } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { SkelettListe } from '@/components/ui/skelett'
import { formatDatum, formatDatumZeit, formatEuro } from '@/lib/format'

/** „Muster GmbH · RE-1 · 01.03.2026 · 119,00 €“ – was vom Beleg bekannt ist. */
export function belegText(g: GeloeschterBeleg): string {
  const teile = [
    g.korrespondent ?? g.titel ?? 'Unbekannter Korrespondent',
    g.rechnungsnummer,
    g.rechnungsdatum && formatDatum(g.rechnungsdatum),
    g.brutto != null && formatEuro(g.brutto, g.waehrung),
  ]
  return teile.filter(Boolean).join(' · ')
}

function Leer() {
  return (
    <div className="flex flex-col items-center gap-1.5 rounded-[10px] border border-dashed border-schalter-aus px-5 py-8 text-center">
      <Icon name="delete" className="text-[36px] text-[#9AA4B5]" />
      <strong>Keine gelöschten Belege</strong>
      <span className="gedaempft text-sm">
        Falsch zugeordnete Belege löscht ein Admin im Beleg über ⋯ → „Löschen (falsch zugeordnet) …“.
      </span>
    </div>
  )
}

function Zeile({ eintrag }: { eintrag: GeloeschterBeleg }) {
  const toast = useToast()
  const wiederherstellen = useBelegWiederherstellen()

  async function zurueckholen() {
    try {
      await wiederherstellen.mutateAsync(eintrag.id)
      toast.erfolg('Beleg wiederhergestellt')
    } catch (f) {
      toast.fehler(f instanceof Error ? f.message : 'Wiederherstellen fehlgeschlagen')
    }
  }

  return (
    <li className="flex flex-wrap items-center justify-between gap-3 border-t border-linie py-3">
      <div className="flex min-w-0 flex-1 flex-col gap-0.5 text-sm">
        <strong className="truncate font-medium text-text">{belegText(eintrag)}</strong>
        <span className="gedaempft text-[13px]">
          Gelöscht am {formatDatumZeit(eintrag.geloeschtAm)}
          {eintrag.geloeschtVon && ` von ${eintrag.geloeschtVon}`} · falsch zugeordnet
        </span>
        {eintrag.bemerkung && <span className="text-[13px] text-gedaempft-stark">„{eintrag.bemerkung}“</span>}
      </div>
      <div className="flex flex-none items-center gap-2">
        {eintrag.originalId && (
          <a
            className="btn btn-klein"
            href={belegDateiUrl(eintrag.id, eintrag.originalId)}
            target="_blank"
            rel="noreferrer"
            aria-label={`Original ansehen: ${belegText(eintrag)}`}
          >
            <Icon name="visibility" />
            Original
          </a>
        )}
        <Button
          type="button"
          size="klein"
          disabled={wiederherstellen.isPending}
          laedt={wiederherstellen.isPending}
          aria-label={`Wiederherstellen: ${belegText(eintrag)}`}
          onClick={() => void zurueckholen()}
        >
          <Icon name="restore_from_trash" />
          Wiederherstellen
        </Button>
      </div>
    </li>
  )
}

function Liste() {
  const { data, isPending, isError, error } = useGeloeschteBelege()
  if (isPending) return <SkelettListe zeilen={3} kompakt symbol={false} label="Gelöschte Belege werden geladen" />
  if (isError)
    return (
      <p role="alert" className="m-0 text-sm text-gefahr">
        {error.message}
      </p>
    )
  if (!data.length) return <Leer />
  return (
    <ul aria-label="Gelöschte Belege" className="m-0 flex list-none flex-col p-0">
      {data.map((g) => (
        <Zeile key={g.id} eintrag={g} />
      ))}
    </ul>
  )
}

/** Bereich „Gelöschte Belege“: als falsch zugeordnet gelöschte Belege ansehen und wiederherstellen (nur Admin). */
export function GeloeschteBelegeBereich() {
  const { istAdmin } = useRolle()
  return (
    <div className="flex flex-col gap-[18px]">
      <h2 className="m-0 text-lg font-semibold text-text">Gelöschte Belege</h2>
      <p className="m-0 text-sm text-gedaempft-stark">
        Belege, die ein Admin als falsch zugeordnet gelöscht hat. Sie sind aus Belegen, Auswertungen und Exporten
        ausgeblendet; ihre Originale liegen in der Ablage unter <span className="mono">_Geloescht</span>. Nach Ablauf
        der Aufbewahrungsfrist werden sie mit ihrem Jahrgang unter{' '}
        <Link to="/einstellungen/aufbewahrung">Aufbewahrung</Link> endgültig gelöscht.
      </p>
      {istAdmin ? (
        <Liste />
      ) : (
        <p className="m-0 text-sm text-gedaempft">Gelöschte Belege sehen und wiederherstellen kann nur ein Admin.</p>
      )}
    </div>
  )
}
