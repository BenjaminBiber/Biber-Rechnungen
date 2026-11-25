import { Link } from '@tanstack/react-router'
import { useState, type ReactNode } from 'react'
import { useKorrespondentAendern } from '@/api/korrespondenten'
import type { KorrespondentAendern, KorrespondentDetail, Matching } from '@/api/typen'
import { MatchingEditor } from '@/components/auswahl/MatchingEditor'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDatum, formatDatumZeit, formatEuro, formatProzent } from '@/lib/format'
import { matchingKurztext } from '@/lib/matching'
import { KorrespondentLogo } from '@/components/KorrespondentLogo'
import { KorrespondentFormular } from '../KorrespondentFormular'
import { alsAenderung, ibanFormatiert, logoQuelleText, websiteHref } from './gemeinsam'
import { ArtikelKarte, BelegeKarte, Kostenverlauf } from './Karten'
import { LogoDialog, type LogoReiter } from './LogoDialog'

/** Speichert eine Teiländerung auf Basis des aktuellen Details mit Toast. */
function useAendern(detail: KorrespondentDetail) {
  const toast = useToast()
  const aendern = useKorrespondentAendern()
  const speichern = (teil: Partial<KorrespondentAendern>, meldung: string) =>
    aendern.mutateAsync({ id: detail.id, ...alsAenderung(detail), ...teil }).then(
      () => {
        toast.erfolg(meldung)
        return true
      },
      (f: Error) => {
        toast.fehler(f.message)
        return false
      },
    )
  return { speichern, laeuft: aendern.isPending }
}

function Aliase({ detail, darfBearbeiten }: { detail: KorrespondentDetail; darfBearbeiten: boolean }) {
  const { speichern, laeuft } = useAendern(detail)
  const [eingabe, setEingabe] = useState<string | null>(null)
  const hinzufuegen = async () => {
    const alias = eingabe?.trim()
    if (!alias) return setEingabe(null)
    if (detail.aliase.includes(alias)) return setEingabe(null)
    if (await speichern({ aliase: [...detail.aliase, alias] }, `Alias „${alias}“ hinzugefügt`)) setEingabe(null)
  }
  return (
    <div className="mt-2 flex flex-wrap items-center gap-1.5 text-[13px]" role="group" aria-label="Aliase">
      <span className="gedaempft">Aliase:</span>
      {detail.aliase.map((a) => (
        <Chip
          key={a}
          ton="grau"
          onEntfernen={
            darfBearbeiten
              ? () => void speichern({ aliase: detail.aliase.filter((x) => x !== a) }, `Alias „${a}“ entfernt`)
              : undefined
          }
        >
          {a}
        </Chip>
      ))}
      {darfBearbeiten &&
        (eingabe === null ? (
          <Button variant="link" size="klein" onClick={() => setEingabe('')}>
            + Alias
          </Button>
        ) : (
          <input
            ref={(el) => el?.focus()}
            aria-label="Neuer Alias"
            className="eingabe h-7 w-48 text-[13px]"
            value={eingabe}
            disabled={laeuft}
            onChange={(e) => setEingabe(e.target.value)}
            onBlur={() => !eingabe.trim() && setEingabe(null)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void hinzufuegen()
              if (e.key === 'Escape') setEingabe(null)
            }}
          />
        ))}
    </div>
  )
}

function Kennzahl({ titel, wert, zusatz }: { titel: string; wert: ReactNode; zusatz?: ReactNode }) {
  return (
    <div className="karte flex-[1_1_200px] p-4">
      <div className="gedaempft text-[13px]">{titel}</div>
      <div className="num mt-1 text-left text-[22px] font-semibold">{wert}</div>
      {zusatz && <div className="gedaempft mt-0.5 text-xs">{zusatz}</div>}
    </div>
  )
}

function Kennzahlen({ detail, jahr }: { detail: KorrespondentDetail; jahr: number }) {
  const k = detail.kennzahlen
  const seit = k.seit ? `seit ${k.seit.slice(5, 7)}/${k.seit.slice(0, 4)}` : undefined
  const faellig = k.naechsteFaelligkeit ? ` · fällig ${formatDatum(k.naechsteFaelligkeit).slice(0, 6)}` : ''
  return (
    <div className="mb-4 flex flex-wrap gap-4" role="group" aria-label="Kennzahlen">
      <Kennzahl
        titel={`Kosten ${jahr}`}
        wert={formatEuro(k.kostenJahr)}
        zusatz={
          k.aenderungVorjahr != null ? `${formatProzent(k.aenderungVorjahr / 100, true)} ggü. ${jahr - 1}` : undefined
        }
      />
      <Kennzahl titel="Belege gesamt" wert={k.anzahlBelege} zusatz={seit} />
      <Kennzahl titel="Ø je Beleg" wert={formatEuro(k.durchschnittJeBeleg)} zusatz={String(jahr)} />
      <Kennzahl
        titel="Offen"
        wert={k.offenAnzahl}
        zusatz={k.offenAnzahl ? `${formatEuro(k.offenSumme)}${faellig}` : undefined}
      />
    </div>
  )
}

function Eintrag({ titel, children }: { titel: string; children: ReactNode }) {
  return (
    <>
      <dt className="gedaempft">{titel}</dt>
      <dd className="m-0 min-w-0 break-words">{children}</dd>
    </>
  )
}

const leer = <span className="gedaempft">—</span>

function Stammdaten({ detail }: { detail: KorrespondentDetail }) {
  return (
    <section className="karte p-5" aria-labelledby="stammdaten-titel">
      <h2 id="stammdaten-titel" className="m-0 mb-4 text-base font-semibold">
        Stammdaten
      </h2>
      <dl className="m-0 grid grid-cols-[140px_minmax(0,1fr)] gap-x-3 gap-y-2.5 text-sm">
        <Eintrag titel="Anschrift">
          {detail.anschrift ? <span className="whitespace-pre-line">{detail.anschrift}</span> : leer}
        </Eintrag>
        <Eintrag titel="USt-IdNr.">{detail.ustIdNr ? <span className="mono">{detail.ustIdNr}</span> : leer}</Eintrag>
        <Eintrag titel="Steuernummer">
          {detail.steuernummer ? <span className="mono">{detail.steuernummer}</span> : leer}
        </Eintrag>
        <Eintrag titel="IBANs">
          {detail.ibans.length
            ? detail.ibans.map((i) => (
                <div key={i} className="mono">
                  {ibanFormatiert(i)}
                </div>
              ))
            : leer}
        </Eintrag>
        <Eintrag titel="Website">
          {detail.website ? (
            <a href={websiteHref(detail.website)} target="_blank" rel="noreferrer">
              {detail.website}
            </a>
          ) : (
            leer
          )}
        </Eintrag>
        <Eintrag titel="E-Mail">{detail.email ?? leer}</Eintrag>
        <Eintrag titel="Logo">{logoQuelleText(detail.logo.quelle)}</Eintrag>
      </dl>
    </section>
  )
}

function MatchingDialog({ detail, onSchliessen }: { detail: KorrespondentDetail; onSchliessen: () => void }) {
  const { speichern, laeuft } = useAendern(detail)
  const [matching, setMatching] = useState<Matching>(detail.matching)
  const sichern = async () => {
    if (await speichern({ matching }, 'Matching gespeichert')) onSchliessen()
  }
  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent className="max-w-[560px]">
        <DialogHeader>
          <DialogTitle>Matching bearbeiten</DialogTitle>
        </DialogHeader>
        <MatchingEditor
          wert={matching}
          onAendern={setMatching}
          ziel="Korrespondent"
          zielId={detail.id}
          zielName={detail.name}
          onSpeichern={sichern}
        />
        <DialogFooter>
          <Button onClick={onSchliessen}>Abbrechen</Button>
          <Button variant="primaer" laedt={laeuft} onClick={() => void sichern()}>
            Speichern
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function Zuordnung({ detail, darfBearbeiten }: { detail: KorrespondentDetail; darfBearbeiten: boolean }) {
  const [offen, setOffen] = useState(false)
  const matching =
    matchingKurztext(detail.matching) +
    (detail.matching.algorithmus === 'Auto' && detail.autoTrainiertAm
      ? ` · trainiert ${formatDatumZeit(detail.autoTrainiertAm)}`
      : '')
  return (
    <section className="karte flex flex-col p-5" aria-labelledby="zuordnung-titel">
      <h2 id="zuordnung-titel" className="m-0 mb-4 text-base font-semibold">
        Zuordnung
      </h2>
      <dl className="m-0 grid grid-cols-[140px_minmax(0,1fr)] gap-x-3 gap-y-2.5 text-sm">
        <Eintrag titel="Default-Kategorie">
          {detail.defaultKategorie ? (
            <>
              {detail.defaultKategorie.name} <span className="gedaempft text-[13px]">pro Beleg überschreibbar</span>
            </>
          ) : (
            leer
          )}
        </Eintrag>
        <Eintrag titel="Matching">{matching}</Eintrag>
        <Eintrag titel="Erkennung">USt-IdNr. › IBAN › Name/Alias</Eintrag>
      </dl>
      {darfBearbeiten && (
        <div className="mt-4">
          <Button size="klein" onClick={() => setOffen(true)}>
            <Icon name="tune" />
            Matching bearbeiten
          </Button>
        </div>
      )}
      {offen && <MatchingDialog detail={detail} onSchliessen={() => setOffen(false)} />}
    </section>
  )
}

/** Detailseite eines Korrespondenten: Kopf mit Logo und Aliasen, Kennzahlen, Karten und Logo-Dialog. */
export function KorrespondentDetailSeite({
  detail,
  istPending,
  jahr,
  logoAus,
  onJahr,
  onLogoAusSchliessen,
}: {
  detail: KorrespondentDetail | undefined
  istPending: boolean
  jahr: number
  logoAus?: string
  onJahr: (jahr: number) => void
  onLogoAusSchliessen: () => void
}) {
  const { darfBearbeiten } = useRolle()
  const [logoReiter, setLogoReiter] = useState<LogoReiter | null>(null)
  const [bearbeiten, setBearbeiten] = useState(false)
  if (istPending) return <main className="seite" aria-busy="true" />
  if (!detail)
    return (
      <main className="seite">
        <h1 className="m-0 text-[22px] font-semibold">Korrespondent nicht gefunden</h1>
        <p className="gedaempft">
          <Link to="/korrespondenten">Zur Korrespondentenliste</Link>
        </p>
      </main>
    )

  const logoOffen = darfBearbeiten && (logoReiter !== null || !!logoAus)
  const typ = [detail.istLieferant && 'Lieferant', detail.istKunde && 'Kunde'].filter(Boolean) as string[]

  return (
    <main className="seite">
      <nav aria-label="Brotkrumen" className="gedaempft mb-2 text-[13px]">
        <Link to="/korrespondenten">Korrespondenten</Link> / <span aria-current="page">{detail.name}</span>
      </nav>
      <div className="mb-6 flex flex-wrap items-start justify-between gap-4">
        <div className="flex min-w-0 items-start gap-4">
          <div className="relative flex-none">
            <KorrespondentLogo logo={detail.logo} groesse={56} />
            {darfBearbeiten && (
              <button
                type="button"
                aria-label="Logo ändern"
                onClick={() => setLogoReiter('ausschnitt')}
                className="absolute -bottom-1.5 -right-1.5 flex size-6 items-center justify-center rounded-full border border-rand bg-karte text-[var(--color-gedaempft-stark)] hover:bg-hover"
              >
                <Icon name="photo_camera" className="!text-[14px]" />
              </button>
            )}
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="m-0 text-[22px] font-semibold text-text">{detail.name}</h1>
              {typ.map((t) => (
                <Chip key={t} ton="blau">
                  {t}
                </Chip>
              ))}
            </div>
            <Aliase key={detail.aliase.join('|')} detail={detail} darfBearbeiten={darfBearbeiten} />
          </div>
        </div>
        {darfBearbeiten && (
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to="/korrespondenten" search={{ zusammenfuehren: detail.id }}>
                <Icon name="merge" />
                Zusammenführen …
              </Link>
            </Button>
            <Button onClick={() => setBearbeiten(true)}>
              <Icon name="edit" />
              Bearbeiten
            </Button>
          </div>
        )}
      </div>
      <Kennzahlen detail={detail} jahr={jahr} />
      <div className="flex flex-col gap-4">
        <div className="grid gap-4 lg:grid-cols-2">
          <Stammdaten detail={detail} />
          <Zuordnung detail={detail} darfBearbeiten={darfBearbeiten} />
        </div>
        <Kostenverlauf detail={detail} jahr={jahr} onJahr={onJahr} />
        <BelegeKarte detail={detail} />
        <ArtikelKarte detail={detail} />
      </div>
      {logoOffen && (
        <LogoDialog
          detail={detail}
          startReiter={logoAus ? 'ausschnitt' : (logoReiter ?? 'ausschnitt')}
          startBelegId={logoAus}
          onSchliessen={() => {
            setLogoReiter(null)
            if (logoAus) onLogoAusSchliessen()
          }}
        />
      )}
      {bearbeiten && <KorrespondentFormular korrespondent={detail} onSchliessen={() => setBearbeiten(false)} />}
    </main>
  )
}
