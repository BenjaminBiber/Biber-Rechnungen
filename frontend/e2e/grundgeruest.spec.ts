import { expect, test } from '@playwright/test'

/** Jede Platzhalter-Route mit der Überschrift, die sie zeigt (`/anmelden` prüft anmeldung.spec.ts). */
const routen: Array<[pfad: string, titel: string]> = [
  ['/', 'Dashboard'],
  ['/benachrichtigungen', 'Benachrichtigungen'],
  ['/belege', 'Belege'],
  ['/belege/b-1', 'Daten'],
  ['/belege/b-1/positionen', 'Positionen'],
  ['/belege/b-1/pruefung', 'Prüfung'],
  ['/belege/b-1/historie', 'Historie'],
  ['/belege/b-1/dateien', 'Dateien'],
  ['/belege/b-1/dubletten', 'Dubletten'],
  ['/belege/b-1/aufteilen', 'Beleg aufteilen'],
  ['/hochladen', 'Hochladen'],
  ['/korrespondenten', 'Korrespondenten'],
  ['/korrespondenten/k-1', 'Korrespondent'],
  ['/artikel', 'Artikel'],
  ['/artikel/a-1', 'Artikel-Detail'],
  ['/artikel/stammartikel/s-1', 'Stammartikel'],
  ['/tags', 'Tags & Kategorien'],
  ['/export', 'Export'],
  ['/einstellungen/allgemein', 'Allgemein'],
  ['/einstellungen/umsatzsteuer', 'Umsatzsteuer'],
  ['/einstellungen/pruefungen', 'Prüfungen'],
  ['/einstellungen/aufbewahrung', 'Aufbewahrung'],
  ['/einstellungen/document-intelligence', 'Document Intelligence'],
  ['/einstellungen/mail', 'E-Mail-Import'],
  ['/einstellungen/eingangsordner', 'Eingangsordner'],
  ['/einstellungen/benutzer', 'Benutzer & Freigaben'],
  ['/einstellungen/api-tokens', 'API-Tokens (MCP)'],
  ['/einstellungen/integritaet', 'Integrität & Sicherung'],
  ['/einstellungen/konto', 'Mein Konto'],
  ['/einstellungen/verfahrensdoku', 'Verfahrensdoku'],
]

test.describe('Grundgerüst', () => {
  for (const [pfad, titel] of routen) {
    test(`${pfad} zeigt „${titel}“`, async ({ page }) => {
      const fehler: string[] = []
      page.on('pageerror', (e) => fehler.push(e.message))

      await page.goto(pfad)
      await expect(page.getByRole('heading', { level: 1, name: titel, exact: true })).toBeVisible()
      expect(fehler).toEqual([])
    })
  }

  test('/einstellungen leitet auf die Prüfungen um', async ({ page }) => {
    await page.goto('/einstellungen')
    await expect(page).toHaveURL(/\/einstellungen\/pruefungen$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Prüfungen', exact: true })).toBeVisible()
  })

  test('unbekannte Pfade zeigen „Seite nicht gefunden“', async ({ page }) => {
    await page.goto('/gibt-es-nicht')
    await expect(page.getByRole('heading', { level: 1, name: 'Seite nicht gefunden' })).toBeVisible()
  })

  test('Schrift und Icons sind lokal eingebunden', async ({ page }) => {
    const fremdeAnfragen: string[] = []
    page.on('request', (r) => {
      if (!r.url().startsWith('http://localhost:4173')) fremdeAnfragen.push(r.url())
    })
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible()
    const schrift = await page.evaluate(() => getComputedStyle(document.body).fontFamily)
    expect(schrift).toContain('IBM Plex Sans')
    expect(fremdeAnfragen).toEqual([])
  })
})
