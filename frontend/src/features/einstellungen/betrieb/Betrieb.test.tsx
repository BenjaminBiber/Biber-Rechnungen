import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { routeTree } from '@/routeTree.gen'
import { backupText, laufText } from './integritaet'
import { mcpBefehl } from './mcpBefehl'

type Anfrage = { methode: string; pfad: string; koerper?: unknown }

/** Rendert die echte App ab `pfad` und merkt sich ändernde Anfragen. */
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

const T = { timeout: 5000 }

describe('Einstellungen Betrieb', { timeout: 20000 }, () => {
  describe('API-Tokens', () => {
    it('zeigt die drei Tokens der Fixtures, der widerrufene mit Chip', async () => {
      const { ende } = renderSeite('/einstellungen/api-tokens')
      expect(await screen.findByText('Claude Desktop', {}, T)).toBeInTheDocument()
      expect(screen.getByText('Claude Code (Laptop)')).toBeInTheDocument()
      expect(screen.getByText('brk_…7f3a')).toBeInTheDocument()
      const test = screen.getByText('Test').closest('tr')!
      expect(within(test).getByText('widerrufen')).toBeInTheDocument()
      expect(within(test).queryByRole('button', { name: /Widerrufen/ })).not.toBeInTheDocument()
      expect(screen.getAllByRole('button', { name: /widerrufen$/ })).toHaveLength(2)
      ende()
    })

    it('Verbinden-Befehl enthält die öffentliche URL und den Platzhalter', async () => {
      const { ende } = renderSeite('/einstellungen/api-tokens')
      const url = db.einstellungen.umgebung.oeffentlicheUrl!
      const pre = await screen.findByLabelText('Befehl zum Verbinden', {}, T)
      await waitFor(() => expect(pre).toHaveTextContent(`${url.replace(/\/+$/, '')}/mcp`))
      expect(pre).toHaveTextContent('Bearer <API-Token>')
      expect(screen.getByText(/^Tools: belege_suchen/)).toBeInTheDocument()
      ende()
    })

    it('Erstellen sendet Name und Scopes, zeigt den Klartext nur im Dialog, Kopieren, nach „Fertig“ weg', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/api-tokens')
      await screen.findByText('Claude Desktop', {}, T)
      await user.click(screen.getByRole('button', { name: 'Token erstellen' }))
      await user.type(screen.getByLabelText('Name'), 'Neuer Laptop')
      await user.click(screen.getByLabelText('upload'))
      await user.click(screen.getByRole('button', { name: 'Erstellen' }))

      const dialog = await screen.findByRole('dialog', { name: 'Token „Neuer Laptop“ erstellt' }, T)
      const feld = within(dialog).getByLabelText<HTMLInputElement>('Token')
      const klartext = feld.value
      expect(klartext).toMatch(/^brk_/)
      expect(anfragen).toContainEqual({
        methode: 'POST',
        pfad: '/api/tokens',
        koerper: { name: 'Neuer Laptop', scopes: ['lesen', 'upload'] },
      })
      expect(within(dialog).getByText(/Nur jetzt sichtbar\./)).toBeInTheDocument()
      const befehl = within(dialog).getByLabelText('Befehl mit Token')
      expect(befehl).toHaveTextContent(`Bearer ${klartext}`)

      await user.click(within(dialog).getByRole('button', { name: 'Kopieren' }))
      expect(await navigator.clipboard.readText()).toBe(klartext)
      await user.click(within(dialog).getByRole('button', { name: 'Befehl kopieren' }))
      expect(await navigator.clipboard.readText()).toBe(befehl.textContent)

      await user.click(within(dialog).getByRole('button', { name: 'Fertig' }))
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
      expect(await screen.findByText('Neuer Laptop', {}, T)).toBeInTheDocument()
      expect(document.body.innerHTML).not.toContain(klartext)
      ende()
    })

    it('Widerrufen mit Rückfrage', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/api-tokens')
      await screen.findByText('Claude Desktop', {}, T)
      await user.click(screen.getByRole('button', { name: 'Claude Desktop widerrufen' }))
      const frage = await screen.findByRole('alertdialog', { name: 'Token „Claude Desktop“ widerrufen?' })
      expect(anfragen).toHaveLength(0)
      await user.click(within(frage).getByRole('button', { name: 'Widerrufen' }))
      await waitFor(() => expect(anfragen.map((a) => a.pfad)).toEqual([`/api/tokens/${db.tokens[0].id}/widerrufen`]))
      await waitFor(() =>
        expect(within(screen.getByText('Claude Desktop').closest('tr')!).getByText('widerrufen')).toBeInTheDocument(),
      )
      ende()
    })

    it('NurLesen kann „upload“ nicht wählen', async () => {
      szenario.rolle('NurLesen')
      const { user, ende } = renderSeite('/einstellungen/api-tokens')
      await screen.findByText('Claude Desktop', {}, T)
      expect(screen.queryByRole('button', { name: 'Claude Desktop widerrufen' })).not.toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Token erstellen' }))
      expect(screen.getByLabelText('upload')).toBeDisabled()
      expect(screen.getByLabelText('lesen')).toBeChecked()
      ende()
    })

    it('mcpBefehl ohne URL nutzt den Platzhalter des Designs', () => {
      expect(mcpBefehl(null, 'x')).toBe(
        'claude mcp add --transport http biber-rechnungen https://rechnungen.<domain>/mcp --header "Authorization: Bearer x"',
      )
      expect(mcpBefehl('http://nas:8080/', 'x')).toContain('http://nas:8080/mcp ')
    })
  })

  describe('Integrität & Sicherung', () => {
    const jetzt = new Date('2026-11-23T12:00:00Z')

    it('Texte der Felder aus den Fixtures', () => {
      expect(laufText(db.integritaetsLaeufe[0], jetzt)).toMatch(
        /^heute \d\d:00 · 2\.184 Dateien · 0 Abweichungen · Dauer 4 Min\.$/,
      )
      expect(backupText(db.backup, jetzt)).toMatch(/^heute \d\d:00 · pg_dump 48 MB · 30 Tage rotierend$/)
      expect(backupText({ ...db.backup, letztesBackup: '2026-11-20T01:00:00Z' }, jetzt)).toBeUndefined()
      expect(backupText({ ...db.backup, letztesBackup: null }, jetzt)).toBeUndefined()
    })

    it('zeigt die Läufe der Fixtures; Admin startet eine Prüfung', async () => {
      const { user, anfragen, ende } = renderSeite('/einstellungen/integritaet')
      const tabelle = await screen.findByRole('table', {}, T)
      expect(within(tabelle).getByText('2.184')).toBeInTheDocument()
      expect(within(tabelle).getAllByText('ok')).toHaveLength(3)
      expect(screen.getByText('Letzte Integritätsprüfung').parentElement).toHaveAttribute('data-ton', 'gruen')
      expect(screen.getByText(/Host-Backup gesichert/)).toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Prüfung jetzt starten' }))
      expect((await screen.findAllByText('Integritätsprüfung gestartet', {}, T)).length).toBeGreaterThan(0)
      expect(anfragen.map((a) => a.pfad)).toEqual(['/api/integritaet/starten'])
      await waitFor(() => expect(within(tabelle).getAllByText('ok')).toHaveLength(4))
      ende()
    })

    it('Lauf mit Abweichung ist rot und aufklappbar', async () => {
      db.integritaetsLaeufe = [
        {
          ...db.integritaetsLaeufe[0],
          anzahlAbweichungen: 2,
          abweichungen: ['2026/11/a.pdf', '2026/11/b.xml'],
        },
        ...db.integritaetsLaeufe.slice(1),
      ]
      const { user, ende } = renderSeite('/einstellungen/integritaet')
      const tabelle = await screen.findByRole('table', {}, T)
      expect(within(tabelle).getByText('Abweichung')).toBeInTheDocument()
      expect(screen.getByText('Letzte Integritätsprüfung').parentElement).toHaveAttribute('data-ton', 'rot')
      const aufklappen = within(tabelle).getByRole('button', { expanded: false })
      expect(screen.queryByText('2026/11/a.pdf')).not.toBeInTheDocument()
      await user.click(aufklappen)
      expect(aufklappen).toHaveAttribute('aria-expanded', 'true')
      const liste = screen.getByRole('list', { name: 'Abweichende Dateien' })
      expect(within(liste).getByText('2026/11/a.pdf')).toBeInTheDocument()
      expect(within(liste).getByText('2026/11/b.xml')).toBeInTheDocument()
      ende()
    })

    it('„Prüfung jetzt starten“ nur für Admins; Backup-Warnung ohne aktuelles Backup', async () => {
      szenario.rolle('Benutzer')
      db.backup = { ...db.backup, letztesBackup: null }
      const { ende } = renderSeite('/einstellungen/integritaet')
      await screen.findByRole('table', {}, T)
      expect(screen.queryByRole('button', { name: 'Prüfung jetzt starten' })).not.toBeInTheDocument()
      expect(await screen.findByText('Kein aktuelles Datenbank-Backup')).toBeInTheDocument()
      expect(screen.getByText('Letztes DB-Backup').parentElement).toHaveAttribute('data-ton', 'orange')
      ende()
    })
  })

  describe('Verfahrensdoku', () => {
    it('zeigt Version, Stand, offene Stellen und PDF-Link', async () => {
      const { ende } = renderSeite('/einstellungen/verfahrensdoku')
      expect(await screen.findByText('Version 0.1', {}, T)).toBeInTheDocument()
      expect(screen.getByText('Stand 23.11.2026 · enthält noch 3 offene [ERGÄNZEN]-Stellen')).toBeInTheDocument()
      const link = screen.getByRole('link', { name: /Als PDF anzeigen/ })
      expect(link.getAttribute('href')).toMatch(/\/api\/verfahrensdoku\/pdf$/)
      expect(link).toHaveAttribute('target', '_blank')
      expect(screen.getByText('v0.4.0')).toBeInTheDocument()
      expect(screen.getByText('aktuell')).toBeInTheDocument()
      ende()
    })

    it('ohne offene Stellen kein Hinweis', async () => {
      db.verfahrensdoku = { ...db.verfahrensdoku, offeneStellen: 0 }
      const { ende } = renderSeite('/einstellungen/verfahrensdoku')
      expect(await screen.findByText('Stand 23.11.2026', {}, T)).toBeInTheDocument()
      ende()
    })

    it('404 → „Verfahrensdokumentation nicht gefunden“', async () => {
      szenario.fehler('/api/verfahrensdoku', 404, 'GET')
      const { ende } = renderSeite('/einstellungen/verfahrensdoku')
      expect(await screen.findByText('Verfahrensdokumentation nicht gefunden', {}, T)).toBeInTheDocument()
      ende()
    })
  })
})
