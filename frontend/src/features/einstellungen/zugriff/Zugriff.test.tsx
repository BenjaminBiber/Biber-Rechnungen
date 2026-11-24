import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { mockZugang } from '@/mocks/fixtures/auth'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { zuletztAktiv } from './BenutzerBereich'

type Anfrage = { methode: string; pfad: string; koerper?: unknown }

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen an `/api/…`. */
function renderSeite(pfad: string) {
  const anfragen: Anfrage[] = []
  const mitschnitt = ({ request }: { request: Request }) => {
    if (request.method === 'GET') return
    const eintrag: Anfrage = { methode: request.method, pfad: new URL(request.url).pathname }
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

const tabelle = () => screen.findByRole('table', { name: 'Benutzer' }, { timeout: 5000 })
const zeile = (name: string) => screen.getByRole('row', { name: new RegExp(name) })
const anfrage = async (anfragen: Anfrage[], pfad: RegExp) => {
  await waitFor(() => expect(anfragen.find((a) => pfad.test(a.pfad))).toBeDefined())
  return anfragen.find((a) => pfad.test(a.pfad))!
}

describe('Benutzer & Freigaben', { timeout: 20000 }, () => {
  it('zeigt die vier Benutzer mit 2FA- und Status-Chips', async () => {
    const { ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    expect(screen.getAllByRole('row')).toHaveLength(5)
    expect(within(zeile('Martin Keller')).getByText('TOTP')).toHaveClass('chip-gruen')
    expect(within(zeile('Anna Lehmann')).getByText('über Google')).toHaveClass('chip-grau')
    expect(within(zeile('Anna Lehmann')).getByText('20.11.2026')).toBeInTheDocument()
    expect(within(zeile('j.wagner')).getByText('nicht eingerichtet')).toHaveClass('chip-grau')
    expect(within(zeile('j.wagner')).getByText('wartet auf Freigabe')).toHaveClass('chip-orange')
    expect(within(zeile('Steuerbüro')).getByText('aktiv')).toHaveClass('chip-gruen')
    ende()
  })

  it('„Zuletzt aktiv“: jetzt unter fünf Minuten, sonst Datum oder Strich', () => {
    const jetzt = Date.parse('2026-11-24T10:00:00Z')
    expect(zuletztAktiv('2026-11-24T09:57:00Z', jetzt)).toBe('jetzt')
    expect(zuletztAktiv('2026-11-20T15:42:00Z', jetzt)).toBe('20.11.2026')
    expect(zuletztAktiv(null, jetzt)).toBe('—')
  })

  it('Freigeben mit gewählter Rolle sendet die Rolle und entfernt den Balken', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    const balken = screen.getByRole('alert')
    expect(balken).toHaveTextContent('Jonas Wagner hat sich registriert und wartet auf Freigabe.')
    const rolle = within(balken).getByRole('combobox', { name: 'Rolle für Jonas Wagner' })
    expect(rolle).toHaveValue('Benutzer')
    await user.selectOptions(rolle, 'NurLesen')
    await user.click(within(balken).getByRole('button', { name: 'Freigeben' }))
    const a = await anfrage(anfragen, /freigeben$/)
    await waitFor(() => expect(a.koerper).toEqual({ rolle: 'NurLesen' }))
    await waitFor(() => expect(screen.queryByText(/wartet auf Freigabe\./)).not.toBeInTheDocument())
    expect(within(zeile('j.wagner')).getByText('aktiv')).toBeInTheDocument()
    ende()
  })

  it('Ablehnen fragt nach und entfernt das Konto', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    await user.click(within(screen.getByRole('alert')).getByRole('button', { name: 'Ablehnen' }))
    const frage = await screen.findByRole('alertdialog', { name: 'Registrierung von Jonas Wagner ablehnen?' })
    expect(anfragen).toHaveLength(0)
    await user.click(within(frage).getByRole('button', { name: 'Ablehnen' }))
    await anfrage(anfragen, /ablehnen$/)
    await waitFor(() => expect(screen.queryByText('j.wagner@firma.de')).not.toBeInTheDocument())
    ende()
  })

  it('Benutzer anlegen lehnt ein zu kurzes Passwort ab und legt sonst an', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    await user.click(screen.getByRole('button', { name: 'Benutzer anlegen' }))
    const dialog = await screen.findByRole('dialog', { name: 'Benutzer anlegen' })
    await user.type(within(dialog).getByLabelText('Name'), 'Eva Neu')
    await user.type(within(dialog).getByLabelText('E-Mail-Adresse'), 'e.neu@firma.de')
    await user.type(within(dialog).getByLabelText('Passwort'), 'kurz')
    await user.selectOptions(within(dialog).getByLabelText('Rolle'), 'Admin')
    await user.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    expect(within(dialog).getByText('Mindestens 12 Zeichen.')).toBeInTheDocument()
    expect(anfragen).toHaveLength(0)
    await user.type(within(dialog).getByLabelText('Passwort'), '-lang-genug-1')
    await user.click(within(dialog).getByRole('button', { name: 'Anlegen' }))
    const a = await anfrage(anfragen, /^\/api\/benutzer$/)
    await waitFor(() =>
      expect(a.koerper).toEqual({
        name: 'Eva Neu',
        email: 'e.neu@firma.de',
        passwort: 'kurz-lang-genug-1',
        rolle: 'Admin',
      }),
    )
    expect(await screen.findByText('e.neu@firma.de')).toBeInTheDocument()
    ende()
  })

  it('Rollenwechsel in der Tabelle; der eigene Eintrag ist gesperrt', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    expect(within(zeile('Martin Keller')).getByRole('combobox')).toBeDisabled()
    await user.click(within(zeile('Martin Keller')).getByRole('button', { name: 'Aktionen für Martin Keller' }))
    expect(await screen.findByRole('menuitem', { name: 'Deaktivieren' })).toHaveAttribute('aria-disabled', 'true')
    await user.keyboard('{Escape}')
    await user.selectOptions(within(zeile('Anna Lehmann')).getByRole('combobox'), 'Admin')
    const a = await anfrage(anfragen, /^\/api\/benutzer\/[^/]+$/)
    await waitFor(() => expect(a.koerper).toEqual({ rolle: 'Admin', aktiv: true }))
    await waitFor(() => expect(within(zeile('Anna Lehmann')).getByRole('combobox')).toHaveValue('Admin'))
    ende()
  })

  it('Deaktivieren über das Zeilenmenü', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    await user.click(within(zeile('Steuerbüro')).getByRole('button', { name: 'Aktionen für Steuerbüro' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Deaktivieren' }))
    const a = await anfrage(anfragen, /^\/api\/benutzer\/[^/]+$/)
    await waitFor(() => expect(a.koerper).toEqual({ rolle: 'NurLesen', aktiv: false }))
    expect(await within(zeile('Steuerbüro')).findByText('deaktiviert')).toHaveClass('chip-grau')
    ende()
  })

  it('Serverfehler 409 (letzter Admin) erscheint als Toast', async () => {
    szenario.fehler('/api/benutzer/{id}', 409, 'PUT')
    const { user, ende } = renderSeite('/einstellungen/benutzer')
    await tabelle()
    await user.selectOptions(within(zeile('Anna Lehmann')).getByRole('combobox'), 'NurLesen')
    expect((await screen.findAllByText('Erzwungener Fehler 409 (Szenario)')).length).toBeGreaterThan(0)
    ende()
  })

  it('Schalter senden die Gruppe „zugriff“', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/benutzer')
    const schalter = await screen.findByRole('switch', { name: 'Offene Registrierung' }, { timeout: 5000 })
    const vorher = db.einstellungen.zugriff
    await user.click(schalter)
    const a = await anfrage(anfragen, /\/api\/einstellungen\/zugriff$/)
    await waitFor(() => expect(a.koerper).toEqual({ ...vorher, offeneRegistrierung: !vorher.offeneRegistrierung }))
    expect(screen.getByRole('switch', { name: 'Google-Anmeldung' })).toBeEnabled()
    ende()
  })

  it('Google-Schalter ist ohne Konfiguration deaktiviert', async () => {
    db.einstellungen = { ...db.einstellungen, umgebung: { ...db.einstellungen.umgebung, googleKonfiguriert: false } }
    const { ende } = renderSeite('/einstellungen/benutzer')
    const google = await screen.findByRole('switch', { name: 'Google-Anmeldung' }, { timeout: 5000 })
    expect(google).toBeDisabled()
    expect(google).toHaveAttribute('aria-checked', 'false')
    expect(screen.getByText(/Google-Anmeldung ist nicht verfügbar/)).toBeInTheDocument()
    ende()
  })

  it('Nicht-Admins sehen nur den Hinweis', async () => {
    szenario.rolle('Benutzer')
    const { ende } = renderSeite('/einstellungen/benutzer')
    expect(await screen.findByText('Nur Admins können Benutzer verwalten.', {}, { timeout: 5000 })).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Benutzer anlegen' })).not.toBeInTheDocument()
    ende()
  })
})

const kontoForm = () => screen.findByRole('form', { name: 'Name und E-Mail' }, { timeout: 5000 })

describe('Mein Konto', { timeout: 20000 }, () => {
  it('Name und E-Mail speichern', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    const name = screen.getByLabelText('Name')
    expect(name).toHaveValue('Martin Keller')
    await user.clear(name)
    await user.type(name, 'Martin K.')
    await user.click(screen.getByRole('button', { name: 'Speichern' }))
    const a = await anfrage(anfragen, /^\/api\/konto$/)
    await waitFor(() => expect(a.koerper).toEqual({ name: 'Martin K.', email: 'm.keller@firma.de' }))
    expect(screen.getByText('zuletzt geändert am 02.09.2026')).toBeInTheDocument()
    ende()
  })

  it('Passwort ändern lehnt eine abweichende Wiederholung ab', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    await user.click(screen.getByRole('button', { name: 'Ändern' }))
    const dialog = await screen.findByRole('dialog', { name: 'Passwort ändern' })
    await user.type(within(dialog).getByLabelText('Aktuelles Passwort'), mockZugang.passwort)
    await user.type(within(dialog).getByLabelText('Neues Passwort'), 'ein-neues-Passwort-1')
    expect(within(dialog).getByText('Sehr starkes Passwort')).toBeInTheDocument()
    await user.type(within(dialog).getByLabelText('Neues Passwort wiederholen'), 'ein-anderes-Passwort')
    await user.click(within(dialog).getByRole('button', { name: 'Passwort ändern' }))
    expect(within(dialog).getByText('Die Passwörter stimmen nicht überein.')).toBeInTheDocument()
    expect(anfragen).toHaveLength(0)
    ende()
  })

  it('TOTP einrichten in zwei Schritten bis zu den Wiederherstellungscodes', async () => {
    db.konto = { ...db.konto, totpAktiv: false, wiederherstellungscodes: 0 }
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    expect(screen.getByText('nicht eingerichtet')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Einrichten' }))
    const dialog = await screen.findByRole('dialog', { name: 'Zwei-Faktor-Anmeldung einrichten' })
    expect(await within(dialog).findByText('JBSW Y3DP EHPK 3PXP')).toBeInTheDocument()
    expect(within(dialog).getByTitle('QR-Code für die Authenticator-App')).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Weiter' }))
    await user.type(within(dialog).getByLabelText('Code'), mockZugang.totpCode)
    await user.click(within(dialog).getByRole('button', { name: 'Aktivieren' }))
    const a = await anfrage(anfragen, /totp\/aktivieren$/)
    await waitFor(() => expect(a.koerper).toEqual({ code: mockZugang.totpCode }))
    const liste = await screen.findByRole('list', { name: 'Wiederherstellungscodes' })
    expect(within(liste).getAllByRole('listitem').length).toBeGreaterThanOrEqual(8)
    expect(screen.getByRole('button', { name: 'Kopieren' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Herunterladen' })).toBeInTheDocument()
    ende()
  })

  it('Codes neu erzeugen mit Rückfrage zeigt die neuen Codes', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    expect(screen.getByText('aktiv · 8 Wiederherstellungscodes übrig')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Codes neu erzeugen' }))
    const frage = await screen.findByRole('alertdialog', { name: 'Neue Wiederherstellungscodes erzeugen?' })
    await user.click(within(frage).getByRole('button', { name: 'Neu erzeugen' }))
    await anfrage(anfragen, /wiederherstellungscodes$/)
    expect(await screen.findByRole('list', { name: 'Wiederherstellungscodes' })).toBeInTheDocument()
    ende()
  })

  it('TOTP deaktivieren mit Rückfrage', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    await user.click(screen.getByRole('button', { name: 'Deaktivieren' }))
    const frage = await screen.findByRole('alertdialog', { name: 'Zwei-Faktor-Anmeldung deaktivieren?' })
    await user.click(within(frage).getByRole('button', { name: 'Deaktivieren' }))
    await anfrage(anfragen, /totp\/deaktivieren$/)
    expect(await screen.findByRole('button', { name: 'Einrichten' })).toBeInTheDocument()
    ende()
  })

  it('„Andere abmelden“ meldet die übrigen Sitzungen ab', async () => {
    const { user, anfragen, ende } = renderSeite('/einstellungen/konto')
    await kontoForm()
    expect(screen.getByText('2 Geräte')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Andere abmelden' }))
    await anfrage(anfragen, /andere-abmelden$/)
    expect(await screen.findByText('1 Gerät · dieses Gerät')).toBeInTheDocument()
    ende()
  })

  it('Google: „Verknüpfen“ verlinkt den Start, ?google=verknuepft zeigt einen Toast', async () => {
    const { ende } = renderSeite('/einstellungen/konto?google=verknuepft')
    await kontoForm()
    expect(screen.getByRole('link', { name: 'Verknüpfen' }).getAttribute('href')).toContain(
      '/api/auth/google/start?verknuepfen=true',
    )
    expect((await screen.findAllByText('Google-Konto verknüpft')).length).toBeGreaterThan(0)
    ende()
  })
})
