import type { UstStatus, UstvaZeitraum } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Bereich } from './Bereich'
import { EinstellungsFormular } from './EinstellungsFormular'

const stati: { wert: UstStatus; titel: string; beschreibung: string; standard?: boolean }[] = [
  {
    wert: 'Regelbesteuerung',
    titel: 'Regelbesteuerung',
    beschreibung: 'Vorsteuer-Ansichten sichtbar, volle § 14-Prüfung',
  },
  {
    wert: 'Kleinunternehmer',
    titel: 'Kleinunternehmer (§ 19 UStG)',
    beschreibung: 'Vorsteuer-Ansichten ausgeblendet, § 14-Prüfung aus',
  },
  {
    wert: 'Unbekannt',
    titel: 'Unbekannt',
    beschreibung: 'wie Regelbesteuerung, bis der Status geklärt ist',
    standard: true,
  },
]

const zeitraeume: { wert: UstvaZeitraum; text: string }[] = [
  { wert: 'Quartalsweise', text: 'Quartalsweise' },
  { wert: 'Monatlich', text: 'Monatlich' },
  { wert: 'Jaehrlich', text: 'Jährlich' },
]

/** Bereich „Umsatzsteuer“: USt-Status als Radio-Karten und UStVA-Zeitraum. */
export function UmsatzsteuerBereich() {
  return (
    <Bereich titel="Umsatzsteuer">
      {(e) => (
        <EinstellungsFormular gruppe="ust" titel="Umsatzsteuer" wert={e.ust}>
          {({ wert, setze }) => (
            <div className="flex flex-col gap-3.5">
              <p className="gedaempft m-0 text-sm">
                Steuert die §-14-Prüfung, die Vorsteuer-Kachel im Dashboard und den Vorsteuer-Export.
              </p>
              <div role="radiogroup" aria-label="Umsatzsteuer-Status" className="flex flex-col gap-3.5">
                {stati.map((s) => {
                  const gewaehlt = wert.status === s.wert
                  return (
                    <label
                      key={s.wert}
                      className={`flex cursor-pointer items-start gap-2.5 rounded-[10px] border px-3.5 py-3 text-sm ${
                        gewaehlt ? 'border-[#A9A8D6] bg-akzent-zart' : 'border-rand'
                      }`}
                    >
                      <input
                        type="radio"
                        name="ust-status"
                        value={s.wert}
                        checked={gewaehlt}
                        onChange={() => setze({ status: s.wert })}
                        aria-describedby={`ust-${s.wert}`}
                      />
                      <span>
                        <strong>{s.titel}</strong> {s.standard && <Chip ton="grau">Standard</Chip>}
                        <br />
                        <span id={`ust-${s.wert}`} className="gedaempft text-[13px]">
                          {s.beschreibung}
                        </span>
                      </span>
                    </label>
                  )
                })}
              </div>
              <div className="max-w-[320px]">
                <label className="label" htmlFor="ust-zeitraum">
                  UStVA-Zeitraum
                </label>
                <select
                  id="ust-zeitraum"
                  className="eingabe"
                  value={wert.zeitraum}
                  onChange={(ev) => setze({ zeitraum: ev.target.value as UstvaZeitraum })}
                >
                  {zeitraeume.map((z) => (
                    <option key={z.wert} value={z.wert}>
                      {z.text}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          )}
        </EinstellungsFormular>
      )}
    </Bereich>
  )
}
