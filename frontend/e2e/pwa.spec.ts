import { expect, test, type BrowserContext, type Page } from '@playwright/test'

/**
 * Projekt `pwa`: echter Build ohne Mock-Server (`npm run preview:pwa`) mit Service Worker.
 * Die API bildet `context.route` nach – das greift auch für Anfragen des Service Workers.
 * `offline` lässt jede API-Anfrage wie ohne Netz scheitern, zusätzlich zu `context.setOffline`.
 */
type Api = { anfragen: string[]; offline: boolean }

// Eigene kleine Testdaten: Die Mock-Fixtures importieren Typen über den Alias `@/`, den tsconfig.node nicht kennt.
const authStatus = {
  eingerichtet: true,
  angemeldet: true,
  benutzer: {
    id: '00000000-0000-4000-8000-0000000000b1',
    name: 'Martin Keller',
    email: 'm.keller@firma.de',
    rolle: 'Admin',
    freigegeben: true,
    totpAktiv: false,
    anmeldung: 'Passwort',
    zuletztAktiv: '2026-11-24T09:00:00Z',
  },
  registrierungOffen: false,
  googleAktiv: false,
}

const belege = ['Hostwerk Süd GmbH', 'Bürowelt Krämer KG'].map((name, i) => ({
  id: `00000000-0000-4000-8000-00000000000${i + 1}`,
  rechnungsnummer: `RE-${1000 + i}`,
  korrespondent: { id: `00000000-0000-4000-8000-0000000000a${i + 1}`, name },
  logo: { url: null, initialen: name.slice(0, 2).toUpperCase(), quelle: 'Keine' },
  titel: `Rechnung ${name}`,
  kategorie: null,
  rechnungsdatum: '2026-11-20',
  faelligkeitsdatum: '2026-12-04',
  ueberfaellig: false,
  netto: 100,
  brutto: 119,
  waehrung: 'EUR',
  seitenanzahl: 1,
  quelle: 'Upload',
  format: 'PDF',
  tags: [],
  auszug: '',
  vorschauUrl: null,
  istDublette: false,
  konfidenz: null,
  leistungsdatum: null,
}))

const vorgang = {
  id: '00000000-0000-4000-8000-0000000000c1',
  zeitpunkt: '2026-11-24T09:00:00Z',
  quelle: 'Upload',
  dateiname: 'Offline_Rechnung.pdf',
  groesse: 16,
  herkunft: 'M. Keller',
  herkunftDetail: null,
  status: 'Fertig',
  versuche: 1,
  maxVersuche: 5,
  ergebnis: null,
  fehler: null,
  belegId: belege[0].id,
  duplikatVonBelegId: null,
  naechsterVersuchAm: null,
  schritte: [],
}

async function apiNachbilden(context: BrowserContext): Promise<Api> {
  const api: Api = { anfragen: [], offline: false }
  await context.route(
    (url) => url.pathname.startsWith('/api/'),
    async (route) => {
      const anfrage = route.request()
      const pfad = new URL(anfrage.url()).pathname
      api.anfragen.push(`${anfrage.method()} ${pfad}`)
      if (api.offline) return route.abort('internetdisconnected')
      const json = (daten: unknown, status = 200) => route.fulfill({ status, json: daten })
      if (anfrage.method() === 'POST' && pfad === '/api/import/upload') return json([vorgang], 202)
      if (anfrage.method() !== 'GET') return json({}, 200)
      switch (pfad) {
        case '/api/auth/status':
          return json(authStatus)
        case '/api/belege':
          return json({
            eintraege: belege,
            gesamt: belege.length,
            seite: 1,
            seitengroesse: 25,
            summeBrutto: 238,
          })
        case '/api/belege/zaehler':
          return json({ alle: belege.length, inbox: 0, zuPruefen: 0, offen: 0, ueberfaellig: 0 })
        case `/api/import/vorgaenge/${vorgang.id}`:
          return json(vorgang)
        default:
          return json({ type: 'about:blank', title: 'Nicht gefunden', status: 404 }, 404)
      }
    },
  )
  return api
}

/** Wartet, bis der Service Worker aktiv ist und die Seite steuert. */
async function serviceWorkerBereit(page: Page): Promise<void> {
  await page.waitForFunction(async () => {
    const registrierung = await navigator.serviceWorker.ready
    return registrierung.active?.state === 'activated' && !!navigator.serviceWorker.controller
  })
}

async function offline(context: BrowserContext, api: Api, aus: boolean) {
  api.offline = aus
  await context.setOffline(aus)
}

test('Manifest ist erreichbar und enthält Share-Target, Icons und display: standalone', async ({ request }) => {
  const antwort = await request.get('/manifest.webmanifest')
  expect(antwort.ok()).toBe(true)
  const manifest = await antwort.json()
  expect(manifest).toMatchObject({
    name: 'Biber-Rechnungen',
    short_name: 'Belege',
    display: 'standalone',
    start_url: '/',
    theme_color: '#2F2D86',
    background_color: '#F4F5F7',
    lang: 'de',
    share_target: {
      action: '/geteilt',
      method: 'POST',
      enctype: 'multipart/form-data',
      params: { files: [{ name: 'dateien', accept: expect.arrayContaining(['application/pdf', 'text/xml']) }] },
    },
  })
  const groessen = manifest.icons.map((i: { sizes: string }) => i.sizes)
  expect(groessen).toEqual(expect.arrayContaining(['192x192', '512x512']))
  expect(manifest.icons.some((i: { purpose?: string }) => i.purpose === 'maskable')).toBe(true)
  for (const icon of manifest.icons) expect((await request.get(icon.src)).ok()).toBe(true)
})

test('App-Shell und zuletzt geladene Liste sind offline verfügbar', async ({ page, context }) => {
  const api = await apiNachbilden(context)
  await page.goto('/belege')
  await serviceWorkerBereit(page)
  // Einmal unter Kontrolle des Service Workers laden, damit die Liste im Cache landet.
  await page.reload()
  // Die mobile Belegliste zeigt je Eintrag den Korrespondenten, nicht den Titel.
  const erster = page.getByRole('list', { name: 'Belege' }).getByText(belege[0].korrespondent.name)
  await expect(erster).toBeVisible()

  await offline(context, api, true)
  await page.goto('/belege')
  await expect(erster).toBeVisible()
  await offline(context, api, false)
})

test('offline gewählte Datei wartet und wird nach Rückkehr des Netzes gesendet', async ({ page, context }) => {
  const api = await apiNachbilden(context)
  await page.goto('/hochladen')
  await serviceWorkerBereit(page)
  await page.reload()
  await expect(page.getByLabel('Dateien auswählen')).toBeAttached()

  await offline(context, api, true)
  await expect(page.getByRole('status').filter({ hasText: 'Offline – Uploads werden gesendet' })).toBeVisible()
  await page.getByLabel('Dateien auswählen').setInputFiles({
    name: 'Offline_Rechnung.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4 offline'),
  })
  const warteschlange = page.getByRole('region', { name: 'Warteschlange' })
  await expect(warteschlange.getByText('Offline_Rechnung.pdf')).toBeVisible()
  await expect(warteschlange.getByRole('img', { name: 'wartet' })).toBeVisible()
  await expect(warteschlange.getByText('1 wartet')).toBeVisible()
  expect(api.anfragen).not.toContain('POST /api/import/upload')

  await offline(context, api, false)
  await expect.poll(() => api.anfragen.filter((a) => a === 'POST /api/import/upload').length).toBe(1)
  await expect(warteschlange.getByRole('img', { name: 'übertragen' })).toBeVisible()
  await expect(page.getByText('Offline – Uploads werden gesendet')).toBeHidden()
})

test('geteilte Datei landet in der Warteschlange', async ({ page, context }) => {
  const api = await apiNachbilden(context)
  await page.goto('/hochladen')
  await serviceWorkerBereit(page)
  await page.reload()
  await expect(page.getByLabel('Dateien auswählen')).toBeAttached()
  // Senden zurückhalten, damit der Eintrag sichtbar „wartet“.
  await offline(context, api, true)

  // Wie Android beim Teilen: Navigation per POST /geteilt mit multipart/form-data.
  await page.evaluate(() => {
    const formular = document.createElement('form')
    formular.method = 'POST'
    formular.action = '/geteilt'
    formular.enctype = 'multipart/form-data'
    const feld = document.createElement('input')
    feld.type = 'file'
    feld.name = 'dateien'
    const uebergabe = new DataTransfer()
    uebergabe.items.add(new File(['%PDF-1.4 geteilt'], 'Geteilte_Rechnung.pdf', { type: 'application/pdf' }))
    feld.files = uebergabe.files
    formular.append(feld)
    document.body.append(formular)
    formular.submit()
  })
  await expect(page).toHaveURL(/\/hochladen\?geteilt=1$/)
  const warteschlange = page.getByRole('region', { name: 'Warteschlange' })
  await expect(warteschlange.getByText('Geteilte_Rechnung.pdf')).toBeVisible()
  await expect(warteschlange.getByText(/^geteilt · /)).toBeVisible()

  await offline(context, api, false)
  await expect.poll(() => api.anfragen.filter((a) => a === 'POST /api/import/upload').length).toBe(1)
})

test('POST-Anfragen landen nie im Cache', async ({ page, context }) => {
  const api = await apiNachbilden(context)
  await page.goto('/belege')
  await serviceWorkerBereit(page)
  await page.reload()
  await page.evaluate(() =>
    fetch('/api/belege/massenaktion', { method: 'POST', body: '{}', headers: { 'Content-Type': 'application/json' } }),
  )
  await expect.poll(() => api.anfragen).toContain('POST /api/belege/massenaktion')
  const cacheInhalt = () =>
    page.evaluate(async () => {
      const urls: string[] = []
      for (const name of await caches.keys()) {
        if (name.startsWith('workbox-precache')) continue
        for (const anfrage of await (await caches.open(name)).keys()) urls.push(`${anfrage.method} ${anfrage.url}`)
      }
      return urls
    })
  // Der Service Worker schreibt die Antwort erst nach dem Ausliefern in den Cache – darauf warten.
  await expect.poll(async () => (await cacheInhalt()).some((u) => u.includes('/api/belege'))).toBe(true)
  const gecachte = await cacheInhalt()
  expect(gecachte.filter((u) => !u.startsWith('GET '))).toEqual([])
  expect(gecachte.filter((u) => u.includes('massenaktion') || u.includes('/api/import/upload'))).toEqual([])
})
