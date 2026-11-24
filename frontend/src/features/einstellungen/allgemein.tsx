import { useTags } from '@/api/tags'
import { Bereich } from './Bereich'
import { EinstellungsFormular } from './EinstellungsFormular'

const zeitzonen = ['Europe/Berlin', 'Europe/Vienna', 'Europe/Zurich', 'UTC']

/** Bereich „Allgemein“: Firmenname, Währung, Zeitzone, Inbox-Tag, Ablage und Adresse im LAN. */
export function AllgemeinBereich() {
  const { data: tags } = useTags()
  const auswahlTags = (tags ?? []).filter((t) => !t.istSystem)

  return (
    <Bereich titel="Allgemein">
      {(e) => (
        <EinstellungsFormular gruppe="allgemein" titel="Allgemein" wert={e.allgemein}>
          {({ wert, setze }) => (
            <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
              <div>
                <label className="label" htmlFor="a-firmenname">
                  Firmenname (für Exporte)
                </label>
                <input
                  id="a-firmenname"
                  className="eingabe"
                  value={wert.firmenname}
                  onChange={(ev) => setze({ firmenname: ev.target.value })}
                />
              </div>
              <div>
                <label className="label" htmlFor="a-waehrung">
                  Standardwährung
                </label>
                <select id="a-waehrung" className="eingabe" value="EUR" onChange={() => undefined}>
                  <option value="EUR">EUR</option>
                </select>
                <span className="hinweis">Fremdwährungen werden gespeichert, ausgewertet wird nur EUR.</span>
              </div>
              <div>
                <label className="label" htmlFor="a-zeitzone">
                  Zeitzone
                </label>
                <select
                  id="a-zeitzone"
                  className="eingabe"
                  value={wert.zeitzone}
                  onChange={(ev) => setze({ zeitzone: ev.target.value })}
                >
                  {(zeitzonen.includes(wert.zeitzone) ? zeitzonen : [wert.zeitzone, ...zeitzonen]).map((z) => (
                    <option key={z}>{z}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="label" htmlFor="a-inbox">
                  Inbox-Tag
                </label>
                <select
                  id="a-inbox"
                  className="eingabe"
                  value={wert.inboxTagId}
                  onChange={(ev) => setze({ inboxTagId: ev.target.value })}
                >
                  {auswahlTags.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </div>
              <div>
                <label className="label" htmlFor="a-ablage">
                  Ablage
                </label>
                <input id="a-ablage" className="eingabe mono" readOnly value={e.umgebung.ablagePfad} />
                <span className="hinweis">Muster: {'{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnr}.{ext}'}</span>
              </div>
              <div>
                <label className="label" htmlFor="a-lan">
                  Adresse im LAN
                </label>
                <input id="a-lan" className="eingabe mono" readOnly value={e.umgebung.oeffentlicheUrl ?? ''} />
              </div>
            </div>
          )}
        </EinstellungsFormular>
      )}
    </Bereich>
  )
}
