import { Link } from '@tanstack/react-router'
import { useErneutAuslesen } from '@/api/belege'
import type { BelegDetail } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { formatDatum, formatEuro } from '@/lib/format'
import { BestaetigenDialog } from './BestaetigenDialog'
import { istPdf, istZuPruefen, original } from './gemeinsam'
import { MehrMenue } from './MehrMenue'

function Logo({ beleg }: { beleg: BelegDetail }) {
  return beleg.logo.url ? (
    <img src={beleg.logo.url} alt="" className="size-[52px] flex-none rounded-karte object-contain" />
  ) : (
    <span
      aria-hidden
      className="flex size-[52px] flex-none items-center justify-center rounded-karte bg-orange-flaeche text-[17px] font-bold text-orange-text"
    >
      {beleg.logo.initialen}
    </span>
  )
}

/** Kopf des Beleg-Details: Logo, Korrespondent, Chips, Eckdaten und Aktionen. */
export function Kopf({ beleg, darfBearbeiten }: { beleg: BelegDetail; darfBearbeiten: boolean }) {
  const toast = useToast()
  const erneutAuslesen = useErneutAuslesen()
  const aufteilbar = istPdf(original(beleg)) && beleg.seitenanzahl > 1

  async function auslesen() {
    try {
      await erneutAuslesen.mutateAsync(beleg.id)
      toast.info('Beleg wird erneut ausgelesen')
    } catch {
      toast.fehler('Erneut auslesen fehlgeschlagen')
    }
  }

  const eckdaten = [
    beleg.rechnungsdatum && formatDatum(beleg.rechnungsdatum),
    beleg.faelligkeitsdatum && `fällig ${formatDatum(beleg.faelligkeitsdatum)}`,
  ].filter(Boolean)

  return (
    <div className="flex flex-wrap items-center gap-4">
      <Logo beleg={beleg} />
      <div className="min-w-0 flex-[1_1_320px]">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="m-0 text-[22px] font-semibold">{beleg.korrespondent?.name ?? beleg.titel}</h1>
          <Chip ton="grau">{beleg.belegart}</Chip>
          {beleg.tags
            .filter((t) => !t.istSystem)
            .map((t) => (
              <Chip key={t.id} ton={t.farbe as ChipTon}>
                {t.name}
              </Chip>
            ))}
          {beleg.tags
            .filter((t) => t.istSystem)
            .map((t) => (
              <Chip key={t.id} ton="orange" icon="report">
                {t.name}
              </Chip>
            ))}
        </div>
        <div className="gedaempft mt-1 text-sm">
          {beleg.rechnungsnummer && (
            <>
              Nr. <span className="mono text-text">{beleg.rechnungsnummer}</span>
              {' · '}
            </>
          )}
          {eckdaten.map((teil) => `${teil} · `)}
          {beleg.brutto !== null && beleg.brutto !== undefined && (
            <strong className="text-text">{formatEuro(beleg.brutto, beleg.waehrung)}</strong>
          )}
        </div>
      </div>
      <div className="flex flex-wrap gap-2">
        {darfBearbeiten && (
          <button type="button" className="btn" onClick={() => void auslesen()} disabled={erneutAuslesen.isPending}>
            <Icon name="refresh" />
            Erneut auslesen
          </button>
        )}
        {darfBearbeiten && aufteilbar && (
          <Link to="/belege/$belegId/aufteilen" params={{ belegId: beleg.id }} className="btn">
            <Icon name="call_split" />
            Aufteilen
          </Link>
        )}
        <MehrMenue beleg={beleg} darfBearbeiten={darfBearbeiten} />
        {darfBearbeiten && istZuPruefen(beleg) && <BestaetigenDialog beleg={beleg} />}
      </div>
    </div>
  )
}
