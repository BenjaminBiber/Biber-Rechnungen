import { useEffect, useState } from 'react'
import { useKorrespondenten } from '@/api/korrespondenten'
import { Icon } from '@/components/Icon'
import { Seitenkopf } from '@/components/Seitenkopf'
import { useRolle } from '@/components/shell/useRolle'
import { Button } from '@/components/ui/button'
import { korrespondentenTypen, type KorrespondentenTyp } from './gemeinsam'
import { KorrespondentenTabelle } from './KorrespondentenTabelle'
import { KorrespondentFormular } from './KorrespondentFormular'
import { ZusammenfuehrenDialog } from './ZusammenfuehrenDialog'

export type KorrespondentenWahl = {
  suche?: string
  typ?: KorrespondentenTyp
  zusammenfuehren?: string
  neu?: boolean
}

const ENTPRELLUNG_MS = 300

/** Korrespondentenliste mit Suche, Typ-Umschalter, Zusammenführen und Neuanlage. */
export function KorrespondentenSeite({
  suche,
  typ,
  zusammenfuehren,
  neu,
  jahr,
  onWahl,
}: {
  suche: string
  typ: KorrespondentenTyp
  /** IDs der vorausgewählten Korrespondenten – öffnet den Dialog. */
  zusammenfuehren: string[]
  neu: boolean
  jahr: number
  onWahl: (wahl: KorrespondentenWahl) => void
}) {
  const { darfBearbeiten } = useRolle()
  const { data: eintraege = [], isPending } = useKorrespondenten({ suche: suche || undefined, typ, jahr })
  const [eingabe, setEingabe] = useState(suche)
  const [auswahl, setAuswahl] = useState<string[]>([])

  useEffect(() => {
    if (eingabe === suche) return
    const zeit = setTimeout(() => onWahl({ suche: eingabe || undefined }), ENTPRELLUNG_MS)
    return () => clearTimeout(zeit)
  }, [eingabe, suche, onWahl])

  const imDialog = eintraege.filter((k) => zusammenfuehren.includes(k.id))

  return (
    <main className="seite">
      <Seitenkopf
        titel="Korrespondenten"
        aktionen={
          darfBearbeiten && (
            <>
              <Button disabled={auswahl.length < 2} onClick={() => onWahl({ zusammenfuehren: auswahl.join(',') })}>
                <Icon name="merge" />
                {auswahl.length < 2 ? 'Zusammenführen' : `${auswahl.length} zusammenführen`}
              </Button>
              <Button variant="primaer" onClick={() => onWahl({ neu: true })}>
                <Icon name="add" />
                Neu anlegen
              </Button>
            </>
          )
        }
      />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <label className="relative min-w-[260px] flex-1">
          <span className="sr-only">Suche</span>
          <Icon name="search" className="absolute top-1/2 left-2.5 -translate-y-1/2 text-gedaempft" />
          <input
            type="search"
            className="eingabe pl-9"
            placeholder="Name, Alias, USt-IdNr. oder IBAN"
            value={eingabe}
            onChange={(e) => setEingabe(e.target.value)}
          />
        </label>
        <div className="segment" role="group" aria-label="Typ">
          {korrespondentenTypen.map((t) => (
            <button key={t} type="button" aria-pressed={typ === t} onClick={() => onWahl({ typ: t })}>
              {t}
            </button>
          ))}
        </div>
      </div>
      <div className="karte overflow-hidden p-0">
        {isPending ? (
          <p className="gedaempft m-0 p-6 text-sm">Lädt …</p>
        ) : (
          <KorrespondentenTabelle
            eintraege={eintraege}
            typ={typ}
            jahr={jahr}
            auswahl={auswahl}
            onAuswahl={darfBearbeiten ? setAuswahl : undefined}
          />
        )}
      </div>
      {darfBearbeiten && imDialog.length >= 2 && (
        <ZusammenfuehrenDialog
          key={zusammenfuehren.join(',')}
          korrespondenten={imDialog}
          onSchliessen={() => onWahl({ zusammenfuehren: undefined })}
          onZusammengefuehrt={() => {
            setAuswahl([])
            onWahl({ zusammenfuehren: undefined })
          }}
        />
      )}
      {darfBearbeiten && neu && <KorrespondentFormular onSchliessen={() => onWahl({ neu: undefined })} />}
    </main>
  )
}
