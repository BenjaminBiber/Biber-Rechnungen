import { Link } from '@tanstack/react-router'
import { useId, useState } from 'react'
import { useBezugSetzen, useSkontoSetzen } from '@/api/belege'
import { istApiFehler } from '@/api/client'
import type { BelegDetail, BezugVerweis } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Datumseingabe } from '@/components/Datumseingabe'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { formatDatum, formatEuro, formatZahl } from '@/lib/format'
import { datumEingabe, parseDatum, parseSatz } from './gemeinsam'

/** „RE-2026-0815 vom 03.11.2026 · 1.234,56 €“ */
export function verweisText(v: BezugVerweis): string {
  return [
    v.rechnungsnummer ?? 'ohne Nummer',
    v.rechnungsdatum && `vom ${formatDatum(v.rechnungsdatum)}`,
    v.brutto !== null && v.brutto !== undefined && `· ${formatEuro(v.brutto, v.waehrung)}`,
  ]
    .filter(Boolean)
    .join(' ')
}

function BelegVerweis({ verweis }: { verweis: BezugVerweis }) {
  return (
    <Link to="/belege/$belegId" params={{ belegId: verweis.id }}>
      {verweisText(verweis)}
    </Link>
  )
}

/** Skonto einer Rechnung: Anzeige mit Frist und Ersparnis, für Bearbeiter erfassen, ändern und entfernen. */
function Skonto({ beleg, nurLesen }: { beleg: BelegDetail; nurLesen: boolean }) {
  const id = useId()
  const toast = useToast()
  const setzen = useSkontoSetzen()
  const [entwurf, setEntwurf] = useState<{ prozent: string; bis: string } | null>(null)
  const [fehler, setFehler] = useState<{ prozent?: string; bis?: string }>({})
  const skonto = beleg.skonto

  const oeffnen = () => {
    setFehler({})
    setEntwurf({
      prozent: skonto ? formatZahl(skonto.prozent) : '',
      bis: datumEingabe(skonto?.bis),
    })
  }

  const speichern = (prozent: number | null, bis: string | null) =>
    setzen.mutate(
      { id: beleg.id, prozent, bis },
      {
        onSuccess: () => {
          setEntwurf(null)
          toast.erfolg(prozent === null ? 'Skonto entfernt' : 'Skonto gespeichert')
        },
        onError: (f) => {
          const feldFehler = istApiFehler(f) ? f.fehler : undefined
          if (feldFehler) setFehler({ prozent: feldFehler.prozent?.[0], bis: feldFehler.bis?.[0] })
          else toast.fehler(f.message)
        },
      },
    )

  const uebernehmen = () => {
    if (!entwurf) return
    const prozent = parseSatz(entwurf.prozent)
    const bis = entwurf.bis.trim() ? parseDatum(entwurf.bis) : null
    const neu = {
      prozent: prozent === null || prozent <= 0 || prozent > 100 ? 'Prozentsatz wie 2 oder 2,5 eingeben' : undefined,
      bis: entwurf.bis.trim() && !bis ? 'Datum im Format TT.MM.JJJJ eingeben' : undefined,
    }
    setFehler(neu)
    if (!neu.prozent && !neu.bis) speichern(prozent, bis)
  }

  if (entwurf) {
    return (
      <div className="flex flex-wrap items-start gap-2.5" role="group" aria-label="Skonto bearbeiten">
        <label className="flex flex-col gap-1 text-[13px]" htmlFor={`${id}-prozent`}>
          <span className="label">Skonto %</span>
          <input
            id={`${id}-prozent`}
            className="eingabe num w-[90px]"
            inputMode="decimal"
            value={entwurf.prozent}
            aria-invalid={!!fehler.prozent}
            onChange={(e) => setEntwurf({ ...entwurf, prozent: e.target.value })}
          />
          {fehler.prozent && (
            <span role="alert" className="text-gefahr">
              {fehler.prozent}
            </span>
          )}
        </label>
        <label className="flex flex-col gap-1 text-[13px]" htmlFor={`${id}-bis`}>
          <span className="label">Zahlung bis</span>
          <Datumseingabe
            id={`${id}-bis`}
            wert={entwurf.bis}
            aria-invalid={!!fehler.bis}
            onAendern={(t) => setEntwurf({ ...entwurf, bis: t })}
          />
          {fehler.bis && (
            <span role="alert" className="text-gefahr">
              {fehler.bis}
            </span>
          )}
        </label>
        <div className="flex gap-1.5 self-end">
          <Button type="button" variant="primaer" size="klein" onClick={uebernehmen} disabled={setzen.isPending}>
            Speichern
          </Button>
          {skonto && (
            <Button type="button" size="klein" onClick={() => speichern(null, null)} disabled={setzen.isPending}>
              Entfernen
            </Button>
          )}
          <Button type="button" variant="geist" size="klein" onClick={() => setEntwurf(null)}>
            Abbrechen
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      {skonto ? (
        <>
          <span className="flex min-w-0 flex-[1_1_220px] items-start gap-2">
            <Icon name="percent" className="flex-none text-gedaempft" />
            <span>
              <strong>{formatZahl(skonto.prozent)} % Skonto</strong>
              {skonto.bis && <> bis {formatDatum(skonto.bis)}</>}
              {skonto.betrag !== null && skonto.betrag !== undefined && (
                <span className="gedaempft"> · spart {formatEuro(skonto.betrag, beleg.waehrung)}</span>
              )}
            </span>
          </span>
          {skonto.abgelaufen && <Chip ton="grau">abgelaufen</Chip>}
        </>
      ) : (
        <span className="flex min-w-0 flex-[1_1_220px] items-center gap-2">
          <Icon name="percent" className="flex-none text-gedaempft" />
          <span className="gedaempft">Kein Skonto erkannt</span>
        </span>
      )}
      {!nurLesen && (
        <Button type="button" variant="geist" size="klein" onClick={oeffnen}>
          {skonto ? 'Ändern' : 'Skonto erfassen'}
        </Button>
      )}
    </div>
  )
}

/** Gutschrift: Auswahl der Rechnung, auf die sie sich bezieht. */
function RechnungAuswahl({ beleg, nurLesen }: { beleg: BelegDetail; nurLesen: boolean }) {
  const id = useId()
  const toast = useToast()
  const setzen = useBezugSetzen()
  const { rechnung, kandidaten } = beleg.bezug
  // Die verknüpfte Rechnung steht nicht zwingend unter den neuesten Kandidaten.
  const optionen = rechnung && !kandidaten.some((k) => k.id === rechnung.id) ? [rechnung, ...kandidaten] : kandidaten

  const aendern = (bezugBelegId: string | null) =>
    setzen.mutate(
      { id: beleg.id, bezugBelegId },
      {
        onSuccess: () => toast.erfolg(bezugBelegId ? 'Gutschrift der Rechnung zugeordnet' : 'Zuordnung gelöst'),
        onError: (f) =>
          toast.fehler(istApiFehler(f) ? (Object.values(f.fehler ?? {})[0]?.[0] ?? f.message) : f.message),
      },
    )

  return (
    <div className="flex flex-col gap-1.5 text-sm">
      <label className="label" htmlFor={`${id}-rechnung`}>
        Bezieht sich auf Rechnung
      </label>
      {nurLesen ? (
        rechnung ? (
          <BelegVerweis verweis={rechnung} />
        ) : (
          <span className="gedaempft">Keine Rechnung zugeordnet</span>
        )
      ) : (
        <div className="flex flex-wrap items-center gap-2">
          <select
            id={`${id}-rechnung`}
            className="eingabe min-w-0 flex-[1_1_260px]"
            value={rechnung?.id ?? ''}
            disabled={setzen.isPending || (!beleg.korrespondent && !rechnung)}
            onChange={(e) => aendern(e.target.value || null)}
          >
            <option value="">{beleg.korrespondent ? '— keine —' : 'Erst einen Korrespondenten zuordnen'}</option>
            {optionen.map((k) => (
              <option key={k.id} value={k.id}>
                {verweisText(k)}
              </option>
            ))}
          </select>
          {rechnung && (
            <Link to="/belege/$belegId" params={{ belegId: rechnung.id }} className="text-[13px]">
              Öffnen
            </Link>
          )}
        </div>
      )}
    </div>
  )
}

/** Rechnung: die Gutschriften, die sich auf sie beziehen, und der verbleibende Betrag. */
function Gutschriften({ beleg }: { beleg: BelegDetail }) {
  const { gutschriften, restbetrag } = beleg.bezug
  return (
    <div className="flex flex-col gap-1 text-sm" aria-label="Gutschriften zu dieser Rechnung" role="group">
      <span className="label">Gutschriften zu dieser Rechnung</span>
      <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
        {gutschriften.map((g) => (
          <li key={g.id}>
            <BelegVerweis verweis={g} />
          </li>
        ))}
      </ul>
      {restbetrag !== null && restbetrag !== undefined && (
        <span>
          Verbleibender Betrag: <strong>{formatEuro(restbetrag, beleg.waehrung)}</strong>
          {restbetrag <= 0 && <span className="gedaempft"> – durch Gutschriften ausgeglichen</span>}
        </span>
      )}
    </div>
  )
}

/** Zahlung: Skonto (Rechnung) und Bezug zwischen Gutschrift und Rechnung. Speichert jeweils sofort. */
export function ZahlungBereich({ beleg, nurLesen }: { beleg: BelegDetail; nurLesen: boolean }) {
  const istRechnung = beleg.belegart === 'Rechnung'
  const istGutschrift = beleg.belegart === 'Gutschrift'
  if (!istRechnung && !istGutschrift) return null
  return (
    <section className="flex flex-col gap-3 rounded-[10px] border border-linie px-3.5 py-3" aria-label="Zahlung">
      {istRechnung && <Skonto beleg={beleg} nurLesen={nurLesen} />}
      {istRechnung && beleg.bezug.gutschriften.length > 0 && <Gutschriften beleg={beleg} />}
      {istGutschrift && <RechnungAuswahl beleg={beleg} nurLesen={nurLesen} />}
    </section>
  )
}
