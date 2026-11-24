import { Link } from '@tanstack/react-router'
import type { StammartikelListeneintrag } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { formatEuro, formatZahl } from '@/lib/format'
import { AenderungChip } from './AenderungChip'
import { kurzname } from './suche'

/** Acht Balken aus `verlauf`; rein dekorativ, die Textalternative steht daneben. */
function Verlauf({ werte }: { werte: number[] }) {
  if (werte.length === 0) return null
  const max = Math.max(...werte)
  const min = Math.min(...werte)
  const breite = 100 / werte.length
  return (
    <>
      <svg viewBox="0 0 100 32" preserveAspectRatio="none" className="h-8 w-full" aria-hidden="true">
        {werte.map((w, i) => {
          const hoehe = max === min ? 20 : 8 + ((w - min) / (max - min)) * 24
          return (
            <rect
              key={i}
              x={i * breite + 1}
              y={32 - hoehe}
              width={breite - 2}
              height={hoehe}
              rx={1}
              className={i === werte.length - 1 ? 'fill-akzent' : 'fill-akzent-zart'}
            />
          )
        })}
      </svg>
      <span className="sr-only">
        Verlauf: von {formatEuro(werte[0])} auf {formatEuro(werte[werte.length - 1])}
      </span>
    </>
  )
}

/** Raster der Stammartikel als Karten mit Link auf die Stammartikel-Ansicht. */
export function StammartikelKarten({ eintraege }: { eintraege: StammartikelListeneintrag[] }) {
  if (eintraege.length === 0)
    return (
      <div className="karte flex flex-col items-center gap-3 px-6 py-12 text-center">
        <Icon name="category" className="text-[32px] text-gedaempft" />
        <p className="m-0 text-[15px] font-medium">
          Noch keine Stammartikel – markiere im Reiter ‚Artikel‘ zwei gleiche Artikel verschiedener Händler.
        </p>
      </div>
    )
  return (
    <ul className="m-0 grid list-none grid-cols-[repeat(auto-fill,minmax(260px,1fr))] gap-3.5 p-0">
      {eintraege.map((s) => (
        <li key={s.id}>
          <Link
            to="/artikel/stammartikel/$stammartikelId"
            params={{ stammartikelId: s.id }}
            className="karte flex h-full flex-col gap-2.5 p-4 text-text no-underline hover:bg-hover"
          >
            <article aria-label={s.name} className="flex flex-col gap-2.5">
              <div className="flex items-start justify-between gap-2">
                <span className="text-[15px] font-semibold">{s.name}</span>
                <Chip ton="blau">
                  je {formatZahl(s.grundpreisMenge)} {s.basiseinheit}
                </Chip>
              </div>
              <span className="gedaempft text-[13px]">
                {s.anzahlArtikel} Artikel · {s.korrespondenten.join(', ')}
              </span>
              <Verlauf werte={s.verlauf} />
              <div className="flex items-center justify-between gap-2 text-[13px]">
                <span>
                  {s.guenstigsterPreis != null ? (
                    <>
                      Günstigster: {formatEuro(s.guenstigsterPreis)}
                      {s.guenstigsterBei && ` (${kurzname(s.guenstigsterBei)})`}
                    </>
                  ) : (
                    <span className="gedaempft">Noch kein Preis</span>
                  )}
                </span>
                <AenderungChip prozent={s.aenderung} />
              </div>
            </article>
          </Link>
        </li>
      ))}
    </ul>
  )
}
