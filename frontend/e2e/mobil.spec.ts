import { expect, test, type Page } from '@playwright/test'

async function scrolltNichtHorizontal(page: Page) {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }))
  expect(scrollWidth).toBeLessThanOrEqual(clientWidth)
}

test.describe('Mobile Ansichten', () => {
  test.skip(({ isMobile }) => !isMobile, 'nur mobil')

  test('Übersicht → Zu prüfen → Liste → Beleg', async ({ page }) => {
    await page.goto('/?jahr=2026')
    await expect(page.getByText('Gesamtkosten pro Monat')).toBeVisible()
    await scrolltNichtHorizontal(page)

    await page.getByRole('link', { name: /^Zu prüfen\s*5$/ }).click()
    await expect(page).toHaveURL(/\/belege\?ansicht=ZuPruefen/)
    const liste = page.getByRole('list', { name: 'Belege' })
    await expect(liste.getByRole('listitem')).toHaveCount(5)
    await expect(page.getByRole('button', { name: 'Filter, 1 aktiv' })).toBeVisible()
    await scrolltNichtHorizontal(page)

    await liste.getByRole('link', { name: /Bürowelt Krämer KG/ }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Bestätigen' })).toBeVisible()
    await scrolltNichtHorizontal(page)
  })

  test('Filter-Sheet: Offen wählen und anwenden', async ({ page }) => {
    await page.goto('/belege')
    const liste = page.getByRole('list', { name: 'Belege' })
    await expect(liste.getByRole('listitem')).toHaveCount(12)
    await page.getByRole('button', { name: 'Filter' }).click()
    const sheet = page.getByRole('dialog', { name: 'Filtern' })
    await sheet.getByRole('radio', { name: /Offen/ }).click()
    await sheet.getByRole('button', { name: '9 Belege anzeigen' }).click()
    await expect(sheet).toBeHidden()
    await expect(page).toHaveURL(/ansicht=Offen/)
    await expect(liste.getByRole('listitem')).toHaveCount(9)
    await expect(page.getByRole('button', { name: 'Filter, 1 aktiv' })).toBeVisible()
    await scrolltNichtHorizontal(page)
  })
})
