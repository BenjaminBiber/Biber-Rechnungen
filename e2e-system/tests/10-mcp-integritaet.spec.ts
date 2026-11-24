import { expect, test } from '@playwright/test'
import { McpClient } from '../hilfen/mcp'
import { ADMIN_SITZUNG, aufVorgangWarten, einfachesPdf } from '../hilfen/system'
import { ablageDateien, ablageVeraendern } from '../hilfen/werkzeug'

test.use({ storageState: ADMIN_SITZUNG })

const WERKZEUGE = [
  'beleg_abrufen',
  'beleg_hochladen',
  'belege_suchen',
  'korrespondent_abrufen',
  'kosten_auswerten',
  'artikel_preisverlauf',
  'artikel_suchen',
]

test('API-Token → MCP: 7 Tools, Suche, Upload mit Quelle MCP; nach Widerruf 401', async ({ page, baseURL }) => {
  const anlegen = await page.request.post('/api/tokens', { data: { name: 'E2E-Claude', scopes: ['lesen', 'upload'] } })
  expect(anlegen.status(), await anlegen.text()).toBe(201)
  const { token, klartext } = (await anlegen.json()) as { token: { id: string }; klartext: string }
  expect(klartext).toMatch(/^brk_/)

  await page.goto('/einstellungen/api-tokens')
  await expect(page.getByText('E2E-Claude')).toBeVisible()

  const mcp = new McpClient(baseURL!, klartext)
  expect(await mcp.verbinden()).toBe(200)
  expect((await mcp.werkzeuge()).sort()).toEqual([...WERKZEUGE].sort())

  const suche = JSON.stringify(await mcp.werkzeugAufrufen('belege_suchen', { suche: 'SCH-2026-0815' }))
  expect(suche).toContain('SCH-2026-0815')
  expect(suche).toContain('Bürobedarf Schulz GmbH')

  const inhalt = await einfachesPdf('MCP-Upload E2E')
  const hochgeladen = await mcp.werkzeugAufrufen('beleg_hochladen', {
    dateiname: 'mcp-rechnung.pdf',
    inhalt_base64: inhalt.toString('base64'),
  })
  const vorgangId = (hochgeladen.vorgangId ?? hochgeladen.VorgangId) as string
  expect(vorgangId).toBeTruthy()
  const vorgang = await aufVorgangWarten(page.request, vorgangId)
  expect(vorgang.quelle).toBe('Mcp')
  expect(vorgang.status).toBe('Fertig')

  const widerrufen = await page.request.post(`/api/tokens/${token.id}/widerrufen`)
  expect(widerrufen.ok(), await widerrufen.text()).toBeTruthy()
  expect((await new McpClient(baseURL!, klartext).aufrufen('tools/list')).status).toBe(401)
})

test('Integritätsprüfung ok → Datei in der Ablage manipulieren → Abweichung in Glocke und Einstellungen', async ({ page }) => {
  async function pruefen(): Promise<{ id: string; anzahlAbweichungen: number; abweichungen: string[] }> {
    const vorher = ((await (await page.request.get('/api/integritaet/laeufe')).json()) as { id: string }[]).map((l) => l.id)
    await page.goto('/einstellungen/integritaet')
    await page.getByRole('button', { name: 'Prüfung jetzt starten' }).click()
    await expect(page.getByText('Integritätsprüfung gestartet')).toBeVisible()
    let lauf: { id: string; dauerSekunden: number | null; anzahlAbweichungen: number; abweichungen: string[] } | undefined
    await expect
      .poll(
        async () => {
          const laeufe = (await (await page.request.get('/api/integritaet/laeufe')).json()) as NonNullable<typeof lauf>[]
          lauf = laeufe.find((l) => !vorher.includes(l.id) && l.dauerSekunden !== null)
          return lauf !== undefined
        },
        { timeout: 90_000, intervals: [1000] },
      )
      .toBe(true)
    return lauf!
  }

  const ok = await pruefen()
  expect(ok.anzahlAbweichungen).toBe(0)
  await page.reload()
  await expect(page.getByRole('table').getByRole('row').nth(1)).toContainText('ok')

  const opfer = ablageDateien().find((p) => p.endsWith('_SCH-2026-0815.pdf'))!
  expect(opfer).toBeTruthy()
  ablageVeraendern(opfer)

  const befund = await pruefen()
  expect(befund.anzahlAbweichungen).toBe(1)
  expect(befund.abweichungen.join(' ')).toContain('SCH-2026-0815')

  await page.reload()
  const zeile = page.getByRole('table').getByRole('row').nth(1)
  await expect(zeile).toContainText('Abweichung')
  await zeile.getByRole('button').click()
  await expect(page.getByRole('list', { name: 'Abweichende Dateien' })).toContainText('SCH-2026-0815')

  await page.getByRole('button', { name: /^Benachrichtigungen, \d+ ungelesen$/ }).click()
  const glocke = page.getByRole('dialog', { name: 'Benachrichtigungen' })
  await expect(glocke.getByText('Integritätsfehler in der Ablage').first()).toBeVisible()
})
