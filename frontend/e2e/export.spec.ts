import { expect, test } from '@playwright/test'

test('Export erstellen löst einen Download aus und erscheint in „Letzte Exporte“', async ({ page }) => {
  await page.goto('/export?von=2026-10-01&bis=2026-10-31')
  const letzte = page.getByRole('region', { name: 'Letzte Exporte' })
  await expect(letzte.locator('tbody tr')).toHaveCount(3)

  await page.getByRole('button', { name: 'Vorjahr' }).click()
  await expect(page.getByLabel('Von')).toHaveValue(/^01\.01\.\d{4}$/)

  const jahr = new Date().getFullYear() - 1
  await expect(page.getByRole('heading', { name: `Vorschau · Jahr ${jahr}` })).toBeVisible()
  await expect(page.getByRole('complementary').locator('tbody tr').first()).toBeVisible()

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Export erstellen' }).click()
  expect((await download).suggestedFilename()).toMatch(/^Export_.+\.zip$/)

  await expect(letzte.locator('tbody tr')).toHaveCount(4)
  await expect(letzte.locator('tbody tr').first()).toContainText(`Jahr ${jahr}`)
})
