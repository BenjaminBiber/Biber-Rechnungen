import { expect, test } from '@playwright/test'

const karten = [
  'Gesamtkosten pro Monat',
  'Top-Korrespondenten 2026',
  'Offen & überfällig',
  'Ablage & Sicherung',
  'Größte Preisänderungen',
  'Document Intelligence',
]

test.describe('Dashboard', () => {
  test('lädt mit allen Karten', async ({ page, isMobile }) => {
    test.skip(isMobile, 'mobil: e2e/mobil.spec.ts')
    await page.goto('/?jahr=2026')
    await expect(page.getByRole('heading', { level: 1, name: 'Dashboard' })).toBeVisible()
    for (const titel of ['Offen', 'Überfällig', 'Inbox', 'Zu prüfen']) {
      await expect(page.getByRole('link', { name: new RegExp(`^${titel} \\d`) })).toBeVisible()
    }
    await expect(page.getByRole('link', { name: /^Kosten 2026/ })).toBeVisible()
    for (const titel of karten) {
      await expect(page.getByRole('heading', { level: 2, name: titel })).toBeVisible()
    }
    await expect(page.getByRole('heading', { level: 2, name: /^Zu prüfen/ })).toBeVisible()
  })

  test('Klick auf „Zu prüfen“ führt zur gefilterten Belegliste', async ({ page }) => {
    await page.goto('/?jahr=2026')
    await page.getByRole('link', { name: /^Zu prüfen \d/ }).click()
    await expect(page).toHaveURL(/\/belege\?ansicht=ZuPruefen$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Zu prüfen' })).toBeVisible()
  })

  test('hat keine horizontale Bildlaufleiste', async ({ page, isMobile }) => {
    test.skip(isMobile, 'mobil: e2e/mobil.spec.ts')
    await page.goto('/?jahr=2026')
    await expect(page.getByRole('heading', { level: 2, name: 'Document Intelligence' })).toBeVisible()
    const ueberstand = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(ueberstand).toBeLessThanOrEqual(0)
  })
})
