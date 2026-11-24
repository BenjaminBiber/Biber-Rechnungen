import { expect, test } from '@playwright/test'

test('zwei Korrespondenten zusammenführen', async ({ page }) => {
  await page.goto('/korrespondenten')
  const zeilen = page.locator('table tbody tr')
  await expect(zeilen).toHaveCount(10)

  await page.getByRole('checkbox', { name: 'Bürowelt Krämer KG auswählen' }).click()
  await page.getByRole('checkbox', { name: 'Buerowelt Kraemer auswählen' }).click()
  await page.getByRole('button', { name: '2 zusammenführen' }).click()

  const dialog = page.getByRole('dialog', { name: '2 Korrespondenten zusammenführen' })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Zusammenführen' }).click()

  await expect(page.getByText('Korrespondenten zusammengeführt')).toBeVisible()
  await expect(zeilen).toHaveCount(9)
})

test('Klick auf einen Namen öffnet das Detail', async ({ page }) => {
  await page.goto('/korrespondenten')
  await page.getByRole('link', { name: 'Autohaus Brenner GmbH' }).click()
  await expect(page).toHaveURL(/\/korrespondenten\/[0-9a-f-]+$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Korrespondent' })).toBeVisible()
})
