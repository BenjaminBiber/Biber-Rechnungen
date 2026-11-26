import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { balkenFarbe } from './documentIntelligence'
import { vorZeit } from './gemeinsam'
import { mailStatusChip } from './mail'

type Anfrage = { methode: string; pfad: string; koerper?: unknown }

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen an `/api/einstellungen…`. */
function renderSeite(pfad: string) {
  const anfragen: Anfrage[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    const url = new URL(request.url)
    if (request.method === 'GET' || !url.pathname.startsWith('/api/einstellungen')) return
    const eintrag: Anfrage = { methode: request.method, pfad: url.pathname }
    anfragen.push(eintrag)
    void request
      .clone()
      .json()
      .then((k: unknown) => (eintrag.koerper = k))
      .catch(() => undefined)
  }
  server.events.on('request:start', mitschnitt)
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  const router = createRouter({
    routeTree,
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [pfad] }),
  })
  const user = userEvent.setup()
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { user, anfragen, ende: () => server.events.removeListener('request:start', mitschnitt) }
}

const formular = (name: string) => screen.findByRole('form', { name }, { timeout: 5000 })
const knopf = (name: string) => screen.getByRole('button', { name })
const toastMit = async (text: string) =>
  expect((await screen.findAllByText(text, {}, { timeout: 5000 })).length).toBeGreaterThan(0)

describe('Einstellungen Eingang', { timeout: 20000 }, () => {
  describe('Document Intelligence', () => {
    it('zeigt Verbrauch, Warteschlange und Formularwerte der Fixtures', async () => {
      const { ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      expect(await screen.findByText('November 2026')).toBeInTheDocument()
      expect(screen.getByText('412 / 500 Seiten · 82 %')).toBeInTheDocument()
      expect(screen.getByTestId('di-verbrauch-balken')).toHaveAttribute('data-farbe', 'orange')
      expect(screen.getByText(/^Warteschlange: aktiv · 0 wartend · bei 100 % pausiert/)).toBeInTheDocument()
      expect(screen.getByLabelText('Monatslimit (Seiten)')).toHaveValue('500')
      expect(screen.getByLabelText('Benachrichtigen ab')).toHaveValue('80')
      expect(screen.getByLabelText('Max. Seiten je Beleg')).toHaveValue('10')
      expect(screen.getByLabelText('Wiederholungen bei Fehler')).toHaveValue('5 Versuche über ca. 1 Std.')
      expect(screen.getByLabelText('Endpunkt (.env)')).toHaveValue('https://…westeurope.cognitiveservices.azure.com')
      expect(screen.getByLabelText('Modell')).toHaveValue('prebuilt-invoice')
      expect(knopf('Warteschlange jetzt fortsetzen')).toBeDisabled()
      ende()
    })

    it('Balkenfarbe: indigo, orange ab Warnschwelle, rot ab 100 %', () => {
      expect(balkenFarbe(50, 80)).toBe('akzent')
      expect(balkenFarbe(82, 80)).toBe('orange')
      expect(balkenFarbe(100, 80)).toBe('rot')
    })

    it('bei 100 % roter Balken, pausierte Warteschlange mit Anzahl; Fortsetzen meldet Erfolg', async () => {
      db.diVerbrauch = { ...db.diVerbrauch, seiten: 500, prozent: 100, pausiert: true, wartend: 3 }
      const { user, anfragen, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      expect(await screen.findByText('Warteschlange: pausiert · 3 wartend')).toBeInTheDocument()
      expect(screen.getByTestId('di-verbrauch-balken')).toHaveAttribute('data-farbe', 'rot')
      await user.click(knopf('Warteschlange jetzt fortsetzen'))
      await toastMit('Verarbeitung wird fortgesetzt.')
      expect(anfragen.at(-1)?.pfad).toBe('/api/einstellungen/di/fortsetzen')
      ende()
    })

    it('Fortsetzen: Serverfehler erscheint als Fehler-Toast', async () => {
      db.diVerbrauch = { ...db.diVerbrauch, pausiert: true, wartend: 3 }
      szenario.fehler('/api/einstellungen/di/fortsetzen', 500, 'POST')
      const { user, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      await screen.findByText('Warteschlange: pausiert · 3 wartend')
      await user.click(knopf('Warteschlange jetzt fortsetzen'))
      expect(await screen.findByRole('alert', {}, { timeout: 5000 })).toBeInTheDocument()
      ende()
    })

    it('Speichern sendet die Gruppe „di“ mit Zahlen', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      const limit = screen.getByLabelText('Monatslimit (Seiten)')
      await user.clear(limit)
      await user.type(limit, '800')
      await user.click(knopf('Speichern'))
      await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
      expect(anfragen.at(-1)).toMatchObject({
        methode: 'PUT',
        pfad: '/api/einstellungen/di',
        koerper: { monatslimit: 800, warnungAbProzent: 80, maxSeitenJeBeleg: 10 },
      })
      await toastMit('Gespeichert')
      ende()
    })

    it('lehnt Limit −1 und Schwelle 120 % ab, ohne zu senden', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      const limit = screen.getByLabelText('Monatslimit (Seiten)')
      await user.clear(limit)
      await user.type(limit, '-1')
      const schwelle = screen.getByLabelText('Benachrichtigen ab')
      await user.clear(schwelle)
      await user.type(schwelle, '120')
      await user.click(knopf('Speichern'))
      expect(await screen.findByText('Ganze Zahl ab 1 eingeben.')).toBeInTheDocument()
      expect(screen.getByText('Ganze Zahl zwischen 1 und 100 eingeben.')).toBeInTheDocument()
      expect(anfragen).toHaveLength(0)
      ende()
    })

    it('Verbindung testen meldet Erfolg', async () => {
      const { user, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      await user.click(knopf('Verbindung testen'))
      await toastMit('Verbindung zu Document Intelligence erfolgreich.')
      ende()
    })

    it('nicht konfiguriert: Hinweis, Verbindungstest meldet Fehler', async () => {
      db.einstellungen = { ...db.einstellungen, umgebung: { ...db.einstellungen.umgebung, diKonfiguriert: false } }
      const { user, ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      expect(
        screen.getByText(
          'Document Intelligence ist nicht eingerichtet (.env). Belege ohne E-Rechnung werden manuell erfasst.',
        ),
      ).toBeInTheDocument()
      await user.click(knopf('Verbindung testen'))
      await toastMit('Document Intelligence ist nicht konfiguriert.')
      ende()
    })

    it('Nicht-Admin: schreibgeschützt, keine Aktionen', async () => {
      szenario.rolle('Benutzer')
      const { ende } = renderSeite('/einstellungen/document-intelligence')
      await formular('Document Intelligence')
      expect(screen.getByLabelText('Monatslimit (Seiten)')).toBeDisabled()
      expect(screen.queryByRole('button', { name: 'Verbindung testen' })).not.toBeInTheDocument()
      expect(screen.queryByRole('button', { name: 'Warteschlange jetzt fortsetzen' })).not.toBeInTheDocument()
      ende()
    })
  })

  describe('E-Mail-Import', () => {
    const jetzt = Date.parse('2026-11-24T09:00:00Z')
    const basis = {
      mail: {
        aktiv: true,
        abrufintervallMinuten: 5,
        eingangsordner: 'INBOX',
        verarbeitetOrdner: 'V',
        ohneBelegOrdner: 'O',
      },
      mailZugang: { konfiguriert: true } as never,
    }

    it('Status-Chip in allen vier Zuständen', () => {
      expect(mailStatusChip(basis, { verbunden: true, letzterAbruf: '2026-11-24T08:57:00Z' }, jetzt)).toEqual({
        text: 'verbunden · vor 3 Min.',
        ton: 'gruen',
      })
      expect(mailStatusChip(basis, { verbunden: false, fehler: 'Anmeldung abgelehnt' }, jetzt)).toEqual({
        text: 'Fehler: Anmeldung abgelehnt',
        ton: 'rot',
      })
      expect(mailStatusChip(basis, { verbunden: false }, jetzt)).toEqual({ text: 'noch nicht abgerufen', ton: 'grau' })
      expect(mailStatusChip({ ...basis, mailZugang: { konfiguriert: false } as never }, undefined)).toEqual({
        text: 'nicht eingerichtet',
        ton: 'grau',
      })
      expect(mailStatusChip({ ...basis, mail: { ...basis.mail, aktiv: false } }, { verbunden: true })).toEqual({
        text: 'abgeschaltet',
        ton: 'grau',
      })
      expect(vorZeit('2026-11-24T06:00:00Z', jetzt)).toBe('vor 3 Std.')
    })

    it('zeigt Werte, Status-Chip und Hinweistext; Speichern sendet die Gruppe „mail“', async () => {
      db.einstellungen = { ...db.einstellungen, mail: { ...db.einstellungen.mail, aktiv: true } }
      db.mailStatus = { verbunden: true, letzterAbruf: new Date(Date.now() - 3 * 60000).toISOString(), fehler: null }
      const { user, anfragen, ende } = renderSeite('/einstellungen/mail')
      await formular('E-Mail-Import')
      expect(await screen.findByText('verbunden · vor 3 Min.')).toBeInTheDocument()
      expect(screen.getByLabelText('IMAP-Server (.env)')).toHaveValue('imap.example-mail.de:993')
      expect(screen.getByLabelText('Benutzer (.env)')).toHaveValue('rechnungen@firma.de')
      expect(screen.getByText(/Mails werden nie gelöscht/)).toBeInTheDocument()
      await user.selectOptions(screen.getByLabelText('Abrufintervall'), 'alle 15 Minuten')
      await user.click(screen.getByRole('switch', { name: 'E-Mail-Import aktiv' }))
      await user.click(knopf('Speichern'))
      await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
      expect(anfragen.at(-1)).toMatchObject({
        pfad: '/api/einstellungen/mail',
        koerper: { aktiv: false, abrufintervallMinuten: 15, eingangsordner: db.einstellungen.mail.eingangsordner },
      })
      ende()
    })

    it('Fehler-Status als roter Chip', async () => {
      db.einstellungen = { ...db.einstellungen, mail: { ...db.einstellungen.mail, aktiv: true } }
      db.mailStatus = { verbunden: false, fehler: 'Zeitüberschreitung' }
      const { ende } = renderSeite('/einstellungen/mail')
      expect(await screen.findByText('Fehler: Zeitüberschreitung', {}, { timeout: 5000 })).toBeInTheDocument()
      ende()
    })

    it('Jetzt abrufen und Verbindung testen zeigen die Meldung; Fehlerfälle als Fehler-Toast', async () => {
      const { user, ende } = renderSeite('/einstellungen/mail')
      await formular('E-Mail-Import')
      await user.click(knopf('Jetzt abrufen'))
      await toastMit('Postfach abgerufen – keine neuen Mails.')
      await user.click(knopf('Verbindung testen'))
      await toastMit('Verbindung zum Postfach erfolgreich.')
      ende()
    })

    it('Fehlerfälle: IMAP nicht konfiguriert, Abruf mit Serverfehler', async () => {
      db.einstellungen = { ...db.einstellungen, mailZugang: { ...db.einstellungen.mailZugang, konfiguriert: false } }
      szenario.fehler('/api/einstellungen/mail/jetzt-abrufen', 500, 'POST')
      const { user, ende } = renderSeite('/einstellungen/mail')
      await formular('E-Mail-Import')
      expect(screen.getByText('nicht eingerichtet')).toBeInTheDocument()
      await user.click(knopf('Verbindung testen'))
      await toastMit('IMAP ist nicht konfiguriert.')
      await user.click(knopf('Jetzt abrufen'))
      await waitFor(() => expect(screen.getAllByRole('alert').length).toBeGreaterThan(1))
      ende()
    })

    it('IMAP-Zugang aus der .env: nur lesbar, kein Zugangsformular', async () => {
      const { ende } = renderSeite('/einstellungen/mail')
      await formular('E-Mail-Import')
      expect(screen.queryByRole('form', { name: 'IMAP-Zugang' })).not.toBeInTheDocument()
      expect(screen.getByLabelText('IMAP-Server (.env)')).toHaveAttribute('readonly')
      ende()
    })

    it('IMAP-Zugang ohne .env: pflegbar, Passwort bleibt bei leerem Feld', async () => {
      db.einstellungen = {
        ...db.einstellungen,
        mailZugang: {
          ausUmgebung: false,
          host: null,
          port: 993,
          ssl: true,
          benutzer: null,
          passwortGesetzt: false,
          konfiguriert: false,
        },
      }
      const { user, anfragen, ende } = renderSeite('/einstellungen/mail')
      const zugang = await screen.findByRole('form', { name: 'IMAP-Zugang' }, { timeout: 5000 })
      expect(screen.queryByLabelText('IMAP-Server (.env)')).not.toBeInTheDocument()
      expect(screen.getByText('nicht eingerichtet')).toBeInTheDocument()

      // Ohne gespeichertes Passwort ist es Pflicht.
      await user.type(within(zugang).getByLabelText('IMAP-Server'), 'imap.ionos.de')
      await user.type(within(zugang).getByLabelText('Benutzer'), 'rechnungen@firma.de')
      await user.click(within(zugang).getByRole('button', { name: 'Speichern' }))
      expect(within(zugang).getByText('Darf nicht leer sein.')).toBeInTheDocument()

      await user.type(within(zugang).getByLabelText('Passwort'), 'geheim')
      await user.click(within(zugang).getByRole('button', { name: 'Speichern' }))
      await waitFor(() => expect(anfragen.at(-1)?.pfad).toBe('/api/einstellungen/mail/zugang'))
      expect(anfragen.at(-1)).toMatchObject({
        methode: 'PUT',
        koerper: { host: 'imap.ionos.de', port: 993, ssl: true, benutzer: 'rechnungen@firma.de', passwort: 'geheim' },
      })
      await toastMit('Gespeichert')
      await waitFor(() => expect(within(zugang).getByLabelText('Passwort')).toHaveValue(''))
      expect(within(zugang).getByLabelText('Passwort')).toHaveAttribute(
        'placeholder',
        'gespeichert – leer lassen zum Behalten',
      )

      // Nur den Port ändern: Passwort geht als null mit und bleibt gespeichert.
      await user.clear(within(zugang).getByLabelText('Port'))
      await user.type(within(zugang).getByLabelText('Port'), '143')
      await user.click(within(zugang).getByRole('switch', { name: 'SSL/TLS' }))
      await user.click(within(zugang).getByRole('button', { name: 'Speichern' }))
      await waitFor(() => expect(anfragen.at(-1)?.koerper).toMatchObject({ port: 143, ssl: false, passwort: null }))
      ende()
    })

    it('Nicht-Admin: schreibgeschützt, keine Aktionen', async () => {
      szenario.rolle('Benutzer')
      const { ende } = renderSeite('/einstellungen/mail')
      await formular('E-Mail-Import')
      expect(screen.getByLabelText('Eingangsordner')).toBeDisabled()
      expect(screen.queryByRole('button', { name: 'Jetzt abrufen' })).not.toBeInTheDocument()
      ende()
    })
  })

  describe('Eingangsordner', () => {
    it('zeigt Pfad, Freigabe, Formate und „überwacht“; Speichern sendet die Gruppe „ordner“', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/eingangsordner')
      await formular('Eingangsordner')
      expect(await screen.findByText('überwacht')).toBeInTheDocument()
      expect(screen.getByLabelText('Pfad im Container')).toHaveValue('/srv/rechnungen/eingang')
      expect(screen.getByLabelText('Samba-Freigabe')).toHaveValue('Rechnungen-Eingang')
      for (const f of ['PDF', 'XML', 'PNG', 'JPG', 'TIFF']) expect(screen.getByText(f)).toBeInTheDocument()
      const polling = screen.getByLabelText('Polling zusätzlich alle')
      await user.clear(polling)
      await user.type(polling, '60')
      await user.click(knopf('Speichern'))
      await waitFor(() => expect(anfragen.at(-1)?.koerper).toBeDefined())
      expect(anfragen.at(-1)).toMatchObject({
        pfad: '/api/einstellungen/ordner',
        koerper: { pollingSekunden: 60, stabilSekunden: db.einstellungen.ordner.stabilSekunden },
      })
      ende()
    })

    it('Chip „nicht erreichbar“ bei Fehlerstatus', async () => {
      db.systemStatus = db.systemStatus.map((s) =>
        s.schluessel === 'eingangsordner' ? { ...s, schweregrad: 'Fehler' as const } : s,
      )
      const { ende } = renderSeite('/einstellungen/eingangsordner')
      expect(await screen.findByText('nicht erreichbar', {}, { timeout: 5000 })).toBeInTheDocument()
      ende()
    })
  })
})
