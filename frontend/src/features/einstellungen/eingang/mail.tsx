import { useEinstellungen, useMailJetztAbrufen, useMailStatus, useMailVerbindungTesten } from '@/api/einstellungen'
import type { Einstellungen, MailStatus, MailZugang } from '@/api/typen'
import { Chip, type ChipTon } from '@/components/Chip'
import { useRolle } from '@/components/shell/useRolle'
import { EinstellungsFormular } from '../EinstellungsFormular'
import { Schalter } from '../Schalter'
import { Zeile } from '../Zeile'
import { AktionsKnopf, BereichMitStatus, NurLesenFeld, Raster, vorZeit } from './gemeinsam'
import { MailZugangFormular } from './mailZugang'

const intervalle = [5, 15, 30, 60]

/** Status-Chip: nicht eingerichtet, abgeschaltet, Fehler oder verbunden. */
export function mailStatusChip(
  e: Pick<Einstellungen, 'mail' | 'mailZugang'>,
  status: MailStatus | undefined,
  jetzt = Date.now(),
): { text: string; ton: ChipTon } | undefined {
  if (!e.mailZugang.konfiguriert) return { text: 'nicht eingerichtet', ton: 'grau' }
  if (!e.mail.aktiv) return { text: 'abgeschaltet', ton: 'grau' }
  if (!status) return undefined
  if (status.fehler) return { text: `Fehler: ${status.fehler}`, ton: 'rot' }
  if (status.verbunden)
    return {
      text: status.letzterAbruf ? `verbunden · ${vorZeit(status.letzterAbruf, jetzt)}` : 'verbunden',
      ton: 'gruen',
    }
  // Ohne Abruf (z. B. direkt nach dem Aktivieren oder nach verworfenem Fehler) folgt der erste im nächsten Minutentakt.
  if (!status.letzterAbruf) return { text: 'noch nicht abgerufen', ton: 'grau' }
  return { text: 'nicht verbunden', ton: 'grau' }
}

const imapServer = (z: MailZugang) => (z.host ? `${z.host}:${z.port}` : '–')

function StatusChip() {
  const { data: e } = useEinstellungen()
  const { data: status } = useMailStatus()
  const chip = e && mailStatusChip(e, status)
  return chip ? <Chip ton={chip.ton}>{chip.text}</Chip> : null
}

/** Bereich „E-Mail-Import“: IMAP-Postfach, Ordner und Abruf. */
export function MailBereich() {
  const { istAdmin } = useRolle()
  const { refetch } = useMailStatus()
  const testen = useMailVerbindungTesten()
  const abrufen = useMailJetztAbrufen()
  const neuLaden = () => void refetch()

  return (
    <BereichMitStatus titel="E-Mail-Import" status={<StatusChip />}>
      {(e) => (
        <>
          {!e.mailZugang.ausUmgebung && <MailZugangFormular zugang={e.mailZugang} />}
          <EinstellungsFormular gruppe="mail" titel="E-Mail-Import" wert={e.mail}>
            {({ wert, setze, schreibgeschuetzt }) => (
              <div className="flex flex-col gap-4">
                <Zeile titel="E-Mail-Import aktiv" beschreibung="Ruft das Postfach im eingestellten Intervall ab.">
                  <Schalter
                    label="E-Mail-Import aktiv"
                    an={wert.aktiv}
                    disabled={schreibgeschuetzt}
                    onAendern={(an) => setze({ aktiv: an })}
                  />
                </Zeile>
                <Raster>
                  {e.mailZugang.ausUmgebung && (
                    <>
                      <NurLesenFeld id="m-server" label="IMAP-Server (.env)" wert={imapServer(e.mailZugang)} mono />
                      <NurLesenFeld id="m-benutzer" label="Benutzer (.env)" wert={e.mailZugang.benutzer ?? '–'} mono />
                    </>
                  )}
                  <div>
                    <label className="label" htmlFor="m-intervall">
                      Abrufintervall
                    </label>
                    <select
                      id="m-intervall"
                      className="eingabe"
                      value={wert.abrufintervallMinuten}
                      onChange={(ev) => setze({ abrufintervallMinuten: Number(ev.target.value) })}
                    >
                      {intervalle.map((m) => (
                        <option key={m} value={m}>
                          alle {m} Minuten
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="label" htmlFor="m-eingang">
                      Eingangsordner
                    </label>
                    <input
                      id="m-eingang"
                      className="eingabe"
                      value={wert.eingangsordner}
                      onChange={(ev) => setze({ eingangsordner: ev.target.value })}
                    />
                  </div>
                  <div>
                    <label className="label" htmlFor="m-verarbeitet">
                      Nach Verarbeitung verschieben nach
                    </label>
                    <input
                      id="m-verarbeitet"
                      className="eingabe"
                      value={wert.verarbeitetOrdner}
                      onChange={(ev) => setze({ verarbeitetOrdner: ev.target.value })}
                    />
                  </div>
                  <div>
                    <label className="label" htmlFor="m-ohne-beleg">
                      Mails ohne verwertbaren Anhang nach
                    </label>
                    <input
                      id="m-ohne-beleg"
                      className="eingabe"
                      value={wert.ohneBelegOrdner}
                      onChange={(ev) => setze({ ohneBelegOrdner: ev.target.value })}
                    />
                  </div>
                </Raster>
              </div>
            )}
          </EinstellungsFormular>
          <p className="gedaempft m-0 text-[13px]">
            Übernommen werden nur Anhänge (PDF, XML, Bilder). Mails werden nie gelöscht. Die Absender-Domain wird für
            das Favicon gemerkt.
          </p>
          {istAdmin && (
            <div className="flex flex-wrap gap-2">
              <AktionsKnopf aktion={testen} danach={neuLaden}>
                Verbindung testen
              </AktionsKnopf>
              <AktionsKnopf aktion={abrufen} danach={neuLaden}>
                Jetzt abrufen
              </AktionsKnopf>
            </div>
          )}
        </>
      )}
    </BereichMitStatus>
  )
}
