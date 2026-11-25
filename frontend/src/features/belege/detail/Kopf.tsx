import { Link } from '@tanstack/react-router'
import type { BelegDetail, BelegeAbfrage } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { BestaetigenDialog } from './BestaetigenDialog'
import { istPdf, istZuPruefen, original } from './gemeinsam'
import { MehrMenue } from './MehrMenue'
import { Blaettern, ZurueckKnopf } from './Navigation'

/** Kopf des Beleg-Details: Zurück, Logo, Korrespondent, Chips, Blättern und Aktionen (Eckdaten stehen in den Feldern). */
export function Kopf({
  beleg,
  aus,
  darfBearbeiten,
  onOriginalZeigen,
}: {
  beleg: BelegDetail
  aus?: BelegeAbfrage
  darfBearbeiten: boolean
  /** Gesetzt, solange das Original-Panel geschlossen ist: blendet es wieder ein. */
  onOriginalZeigen?: () => void
}) {
  const aufteilbar = istPdf(original(beleg)) && beleg.seitenanzahl > 1

  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
      <div className="flex min-w-0 flex-[1_1_320px] items-center gap-3">
        <ZurueckKnopf aus={aus} />
        <KorrespondentLogo logo={beleg.logo} groesse={36} ton="orange" />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="m-0 text-[22px] font-semibold">{beleg.korrespondent?.name ?? beleg.titel}</h1>
            {/* Laptop-Breiten: ohne Chips – Belegart und Tags stehen im Formular darunter. */}
            <span className="contents max-2xl:hidden">
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
            </span>
          </div>
        </div>
      </div>
      {aus && <Blaettern belegId={beleg.id} aus={aus} />}
      <div className="ml-auto flex flex-wrap justify-end gap-2">
        {onOriginalZeigen && (
          <button type="button" className="btn" onClick={onOriginalZeigen}>
            <Icon name="picture_as_pdf" />
            Original anzeigen
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
