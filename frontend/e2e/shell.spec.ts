import { expect, test } from '@playwright/test'

/** Einträge der Seitenleiste mit Ziel-URL und Überschrift der Zielseite. */
const eintraege: Array<[name: RegExp, url: RegExp, titel: string]> = [
  [/^Belege/, /\/belege$/, 'Alle Belege'],
  [/^Hochladen$/, /\/hochladen$/, 'Hochladen & Import'],
  [/^Korrespondenten$/, /\/korrespondenten$/, 'Korrespondenten'],
  [/^Artikel$/, /\/artikel$/, 'Artikel'],
  [/^Tags & Kategorien$/, /\/tags$/, 'Tags & Kategorien'],
  [/^Export$/, /\/export$/, 'Export'],
  [/^Einstellungen$/, /\/einstellungen\/pruefungen$/, 'Einstellungen'],
  [/^Dashboard$/, /:4173\/$/, 'Dashboard'],
]

test.describe('App-Shell Desktop', () => {
  test.skip(({ isMobile }) => isMobile, 'nur Desktop')

  test('jeder Eintrag der Seitenleiste führt zur richtigen Route', async ({ page }) => {
    await page.goto('/export')
    const nav = page.getByRole('navigation', { name: 'Hauptnavigation' })
    await expect(nav).toBeVisible()
    for (const [name, url, titel] of eintraege) {
      await nav.getByRole('link', { name }).click()
      await expect(page).toHaveURL(url)
      await expect(page.getByRole('heading', { level: 1, name: titel, exact: true })).toBeVisible()
    }
  })

  test('Glocke und Benutzermenü öffnen', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: /^Benachrichtigungen, \d+ ungelesen$/ }).click()
    await expect(page.getByRole('dialog', { name: 'Benachrichtigungen' })).toBeVisible()
    await expect(page.getByText('Document Intelligence bei 82 %')).toBeVisible()
    await page.keyboard.press('Escape')

    await page.getByRole('button', { name: 'Benutzermenü' }).click()
    await expect(page.getByText('Rolle: Admin · 2FA aktiv')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Abmelden' })).toBeVisible()
  })
})

test.describe('App-Shell mobil', () => {
  test.skip(({ isMobile }) => !isMobile, 'nur mobil')

  test('Bottom-Navigation mit fünf Einträgen, Seitenleiste ausgeblendet', async ({ page }) => {
    await page.goto('/')
    const nav = page.getByRole('navigation', { name: 'Mobile Navigation' })
    await expect(nav).toBeVisible()
    await expect(nav.getByRole('link')).toHaveCount(4)
    await expect(nav.getByRole('button', { name: 'Mehr' })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Übersicht' })).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('navigation', { name: 'Hauptnavigation' })).toHaveCount(0)
  })

  test('„Mehr“ öffnet das Sheet mit den übrigen Einträgen', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: 'Mehr' }).click()
    const sheet = page.getByRole('dialog', { name: 'Mehr' })
    await expect(sheet).toBeVisible()
    for (const name of ['Korrespondenten', 'Artikel', 'Tags & Kategorien', 'Export', 'Einstellungen']) {
      await expect(sheet.getByRole('link', { name, exact: true })).toBeVisible()
    }
    await expect(sheet.getByRole('button', { name: 'Abmelden' })).toBeVisible()
    await sheet.getByRole('link', { name: 'Export' }).click()
    await expect(page).toHaveURL(/\/export$/)
  })
})
