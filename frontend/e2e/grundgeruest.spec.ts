import { expect, test } from '@playwright/test'
import { artikelIds, korrespondentIds, stammartikelIds } from '../src/mocks/fixtures/ids'

/** Beleg b2 aus den Mock-Fixtures (Beleg-Detail mit Tabs). */
const beleg = '/belege/b0000000-0000-4000-8000-000000000002'

/** Jede Platzhalter-Route mit der Überschrift, die sie zeigt (`/anmelden` prüft anmeldung.spec.ts). */
const routen: Array<[pfad: string, titel: string]> = [
  ['/', 'Dashboard'],
  ['/benachrichtigungen', 'Benachrichtigungen'],
  ['/belege', 'Alle Belege'],
  ['/belege/b-1', 'Beleg nicht gefunden'],
  [beleg, 'Bürowelt Krämer KG'],
  [`${beleg}/positionen`, 'Positionen'],
  [`${beleg}/pruefung`, 'Prüfung'],
  [`${beleg}/historie`, 'Historie'],
  [`${beleg}/dateien`, 'Dateien'],
  [`${beleg}/dubletten`, 'Dubletten'],
  ['/hochladen', 'Hochladen & Import'],
  ['/korrespondenten', 'Korrespondenten'],
  [`/korrespondenten/${korrespondentIds.bueroweltKraemer}`, 'Bürowelt Krämer KG'],
  ['/artikel', 'Artikel'],
  [`/artikel/${artikelIds.a2}`, 'Ordner breit 8 cm, blau'],
  [`/artikel/stammartikel/${stammartikelIds.kopierpapier}`, 'Kopierpapier A4 80 g'],
  ['/tags', 'Tags & Kategorien'],
  ['/export', 'Export'],
  ['/einstellungen/allgemein', 'Einstellungen'],
  ['/einstellungen/umsatzsteuer', 'Einstellungen'],
  ['/einstellungen/pruefungen', 'Einstellungen'],
  ['/einstellungen/aufbewahrung', 'Einstellungen'],
  ['/einstellungen/document-intelligence', 'Einstellungen'],
  ['/einstellungen/mail', 'Einstellungen'],
  ['/einstellungen/eingangsordner', 'Einstellungen'],
  ['/einstellungen/benutzer', 'Benutzer & Freigaben'],
  ['/einstellungen/api-tokens', 'Einstellungen'],
  ['/einstellungen/integritaet', 'Einstellungen'],
  ['/einstellungen/konto', 'Mein Konto'],
  ['/einstellungen/verfahrensdoku', 'Einstellungen'],
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
    await expect(page.getByRole('heading', { level: 2, name: 'Prüfungen & Schwellen', exact: true })).toBeVisible()
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
