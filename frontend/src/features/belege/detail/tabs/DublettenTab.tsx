import { Link } from '@tanstack/react-router'
import { useState } from 'react'
import { useBeleg, useBelege, useDubletteSetzen } from '@/api/belege'
import type { BelegDetail, BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'
import { useToast } from '@/components/toast'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { SkelettListe } from '@/components/ui/skelett'
import { formatDatum, formatEuro } from '@/lib/format'

function belegName(b: { rechnungsnummer?: string | null; titel: string }): string {
  return b.rechnungsnummer ?? b.titel
}

/** Verweis auf einen Beleg, dessen Nummer erst geladen wird (Teilbelege, „Aufgeteilt aus“). */
function BelegVerweis({ id }: { id: string }) {
  const { data } = useBeleg(id)
  return (
    <Link to="/belege/$belegId" params={{ belegId: id }}>
      {data ? belegName(data) : 'Beleg'}
    </Link>
  )
}

function Abgleich({
  titel,
  treffer,
  ohneTreffer,
  mitTreffer,
  anderer,
}: {
  titel: string
  treffer: boolean
  ohneTreffer: string
  mitTreffer: string
  anderer?: { id: string; name: string }
}) {
  return (
    <div className="flex gap-3 rounded-[10px] border border-rand p-3.5">
      <Icon
        name={treffer ? 'report' : 'check_circle'}
        className={treffer ? 'text-orange-text' : 'text-gruen-text'}
        label={treffer ? 'Treffer' : 'Kein Treffer'}
      />
      <span className="text-sm">
        <strong>{titel}</strong>
        <br />
        <span className="gedaempft text-[13px]">
          {treffer ? (
            <>
              {mitTreffer}
              {anderer && (
                <>
                  {' '}
                  <Link to="/belege/$belegId" params={{ belegId: anderer.id }}>
                    {anderer.name}
                  </Link>
                </>
              )}
            </>
          ) : (
            ohneTreffer
          )}
        </span>
      </span>
    </div>
  )
}

/** Tab „Dubletten“: Datei- und fachlicher Abgleich, Verknüpfungen und „Als Dublette markieren …“. */
export function DublettenTab({
  beleg,
  darfBearbeiten,
  markieren,
  onMarkierenSchliessen,
}: {
  beleg: BelegDetail
  darfBearbeiten: boolean
  markieren: boolean
  onMarkierenSchliessen: () => void
}) {
  const toast = useToast()
  const setzen = useDubletteSetzen()
  const [dialogOffen, setDialogOffen] = useState(false)
  const offen = darfBearbeiten && (dialogOffen || markieren)
  const { duplikatVon, duplikate, hashTreffer, fachlicherTreffer } = beleg.dubletten
  const anderer = duplikatVon ?? (duplikate[0] ? { id: duplikate[0].id, name: belegName(duplikate[0]) } : undefined)

  function schliessen() {
    setDialogOffen(false)
    if (markieren) onMarkierenSchliessen()
  }

  function verknuepfen(ziel: BelegListeneintrag | null) {
    setzen.mutate(
      { id: beleg.id, duplikatVonId: ziel?.id ?? null },
      {
        onSuccess: () => {
          toast.erfolg(ziel ? `Als Dublette von ${belegName(ziel)} markiert` : 'Verknüpfung aufgehoben')
          schliessen()
        },
        onError: () => toast.fehler('Dublette konnte nicht gespeichert werden'),
      },
    )
  }

  return (
    <div className="flex flex-col gap-3">
      {beleg.istDublette && duplikatVon && (
        <div className="flex flex-wrap items-center gap-3 rounded-[10px] bg-orange-flaeche px-3.5 py-3 text-[13px] text-orange-text">
          <Icon name="content_copy" />
          <span className="flex-1">
            Dublette von{' '}
            <Link to="/belege/$belegId" params={{ belegId: duplikatVon.id }}>
              {duplikatVon.name}
            </Link>
          </span>
          {darfBearbeiten && (
            <button
              type="button"
              className="btn btn-klein"
              disabled={setzen.isPending}
              aria-busy={setzen.isPending}
              onClick={() => verknuepfen(null)}
            >
              Verknüpfung aufheben
            </button>
          )}
        </div>
      )}

      <Abgleich
        titel="Datei-Abgleich (SHA-256)"
        treffer={hashTreffer}
        ohneTreffer="Keine identische Datei im Archiv."
        mitTreffer="Identische Datei im Archiv:"
        anderer={anderer}
      />
      <Abgleich
        titel="Fachlicher Abgleich"
        treffer={fachlicherTreffer}
        ohneTreffer="Kein anderer Beleg mit Korrespondent + Rechnungsnr. + Betrag."
        mitTreffer="Gleicher Korrespondent, Rechnungsnr. und Betrag wie"
        anderer={anderer}
      />

      {duplikate.length > 0 && (
        <section aria-labelledby="dubletten-verweise" className="flex flex-col gap-1.5">
          <h3 id="dubletten-verweise" className="m-0 text-sm font-semibold">
            Als Dublette dieses Belegs markiert
          </h3>
          <ul className="m-0 list-none p-0 text-[13px]">
            {duplikate.map((d) => (
              <li key={d.id} className="border-b border-linie py-2">
                <Link to="/belege/$belegId" params={{ belegId: d.id }}>
                  {belegName(d)}
                </Link>{' '}
                <span className="gedaempft">
                  · {d.korrespondent?.name ?? d.titel}
                  {d.rechnungsdatum && ` · ${formatDatum(d.rechnungsdatum)}`}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {beleg.aufgeteiltAusBelegId && (
        <p className="m-0 text-[13px]">
          Aufgeteilt aus <BelegVerweis id={beleg.aufgeteiltAusBelegId} />
        </p>
      )}
      {beleg.teilbelege.length > 0 && (
        <section aria-labelledby="dubletten-teilbelege" className="flex flex-col gap-1.5">
          <h3 id="dubletten-teilbelege" className="m-0 text-sm font-semibold">
            Teilbelege
          </h3>
          <ul className="m-0 list-none p-0 text-[13px]">
            {beleg.teilbelege.map((id) => (
              <li key={id} className="py-1">
                <BelegVerweis id={id} />
              </li>
            ))}
          </ul>
        </section>
      )}

      {darfBearbeiten && !beleg.istDublette && (
        <div className="flex gap-2">
          <button type="button" className="btn" onClick={() => setDialogOffen(true)}>
            Als Dublette markieren …
          </button>
        </div>
      )}

      {offen && (
        <MarkierenDialog
          belegId={beleg.id}
          laeuft={setzen.isPending}
          onWaehlen={verknuepfen}
          onSchliessen={schliessen}
        />
      )}
    </div>
  )
}

function MarkierenDialog({
  belegId,
  laeuft,
  onWaehlen,
  onSchliessen,
}: {
  belegId: string
  laeuft: boolean
  onWaehlen: (ziel: BelegListeneintrag) => void
  onSchliessen: () => void
}) {
  const [suche, setSuche] = useState('')
  const [gewaehlt, setGewaehlt] = useState<BelegListeneintrag | null>(null)
  const { data, isPending, isFetching, isPlaceholderData } = useBelege({ suche: suche.trim() || undefined })
  const treffer = (data?.eintraege ?? []).filter((b) => b.id !== belegId)

  return (
    <Dialog open onOpenChange={(auf) => !auf && onSchliessen()}>
      <DialogContent aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>Als Dublette markieren</DialogTitle>
        </DialogHeader>
        <label className="flex flex-col">
          <span className="label">Original suchen</span>
          <input
            className="eingabe"
            type="search"
            placeholder="Rechnungsnr., Korrespondent, Titel …"
            value={suche}
            onChange={(e) => setSuche(e.target.value)}
            aria-busy={isFetching || undefined}
          />
        </label>
        <ul
          aria-label="Suchtreffer"
          aria-busy={(isPlaceholderData && isFetching) || undefined}
          className="veraltet m-0 flex max-h-72 list-none flex-col overflow-y-auto p-0"
        >
          {isPending && (
            <li>
              <SkelettListe zeilen={4} kompakt label="Suche läuft" />
            </li>
          )}
          {!isPending && treffer.length === 0 && <li className="gedaempft py-2 text-[13px]">Keine Belege gefunden</li>}
          {treffer.map((b) => (
            <li key={b.id}>
              <label className="flex cursor-pointer items-center gap-2.5 border-b border-linie py-2 text-[13px]">
                <input
                  type="radio"
                  name="dubletten-original"
                  checked={gewaehlt?.id === b.id}
                  onChange={() => setGewaehlt(b)}
                />
                <span className="flex-1">
                  <strong>{belegName(b)}</strong>{' '}
                  <span className="gedaempft">
                    · {b.korrespondent?.name ?? b.titel}
                    {b.rechnungsdatum && ` · ${formatDatum(b.rechnungsdatum)}`}
                  </span>
                </span>
                {b.brutto !== null && b.brutto !== undefined && (
                  <span className="num">{formatEuro(b.brutto, b.waehrung)}</span>
                )}
              </label>
            </li>
          ))}
        </ul>
        <DialogFooter>
          <button type="button" className="btn" onClick={onSchliessen}>
            Abbrechen
          </button>
          <button
            type="button"
            className="btn btn-primaer"
            disabled={!gewaehlt || laeuft}
            aria-busy={laeuft}
            onClick={() => gewaehlt && onWaehlen(gewaehlt)}
          >
            Verknüpfen
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
