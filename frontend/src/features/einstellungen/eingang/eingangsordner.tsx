import { useSystemStatus } from '@/api/dashboard'
import type { OrdnerEinstellungen } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { EinstellungsFormular, type Feldfehler } from '../EinstellungsFormular'
import { BereichMitStatus, NurLesenFeld, Raster, ZahlFeld, ganzzahl } from './gemeinsam'

type OrdnerFormular = Record<keyof OrdnerEinstellungen, string>

const formate = ['PDF', 'XML', 'PNG', 'JPG', 'TIFF']

function StatusChip() {
  const { data } = useSystemStatus()
  const eintrag = data?.find((s) => s.schluessel === 'eingangsordner')
  if (!eintrag) return null
  return eintrag.schweregrad === 'Fehler' ? <Chip ton="rot">nicht erreichbar</Chip> : <Chip ton="gruen">überwacht</Chip>
}

function validieren(f: OrdnerFormular): Feldfehler {
  const fehler: Feldfehler = {}
  if (ganzzahl(f.pollingSekunden, 1, 3600) === undefined)
    fehler.pollingSekunden = 'Ganze Zahl zwischen 1 und 3600 eingeben.'
  if (ganzzahl(f.stabilSekunden, 1, 3600) === undefined)
    fehler.stabilSekunden = 'Ganze Zahl zwischen 1 und 3600 eingeben.'
  return fehler
}

/** Bereich „Eingangsordner“: überwachter Ordner, Polling und akzeptierte Formate. */
export function EingangsordnerBereich() {
  return (
    <BereichMitStatus titel="Eingangsordner" status={<StatusChip />}>
      {(e) => (
        <>
          <EinstellungsFormular
            gruppe="ordner"
            titel="Eingangsordner"
            wert={{
              pollingSekunden: String(e.ordner.pollingSekunden),
              stabilSekunden: String(e.ordner.stabilSekunden),
            }}
            validieren={validieren}
            zuWerten={(f) => ({ pollingSekunden: Number(f.pollingSekunden), stabilSekunden: Number(f.stabilSekunden) })}
          >
            {({ wert, setze, fehler }) => (
              <Raster>
                <NurLesenFeld id="o-pfad" label="Pfad im Container" wert={e.umgebung.eingangPfad} mono />
                <NurLesenFeld id="o-freigabe" label="Samba-Freigabe" wert="Rechnungen-Eingang" mono />
                <ZahlFeld
                  id="o-polling"
                  label="Polling zusätzlich alle"
                  einheit="s"
                  wert={wert.pollingSekunden}
                  fehler={fehler.pollingSekunden}
                  onAendern={(v) => setze({ pollingSekunden: v })}
                />
                <ZahlFeld
                  id="o-stabil"
                  label="Übernahme, wenn Größe stabil seit"
                  einheit="s"
                  wert={wert.stabilSekunden}
                  fehler={fehler.stabilSekunden}
                  onAendern={(v) => setze({ stabilSekunden: v })}
                />
              </Raster>
            )}
          </EinstellungsFormular>
          <div>
            <span className="label">Akzeptierte Formate</span>
            <ul aria-label="Akzeptierte Formate" className="m-0 flex list-none flex-wrap gap-1.5 p-0">
              {formate.map((f) => (
                <li key={f}>
                  <Chip ton="grau">{f}</Chip>
                </li>
              ))}
            </ul>
          </div>
        </>
      )}
    </BereichMitStatus>
  )
}
