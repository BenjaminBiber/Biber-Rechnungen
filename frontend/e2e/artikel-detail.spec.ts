import { expect, test } from '@playwright/test'
import { stammartikelIds } from '../src/mocks/fixtures/ids'

test('Stammartikel: Preisart umschalten und Beleg eines Kaufs öffnen', async ({ page }) => {
  await page.goto(`/artikel/stammartikel/${stammartikelIds.kopierpapier}?jahr=2026`)
  await expect(page.getByRole('heading', { level: 1, name: 'Kopierpapier A4 80 g' })).toBeVisible()

  const legende = page.getByRole('list', { name: 'Legende' })
  await expect(legende.getByRole('listitem')).toHaveCount(2)
  await expect(legende).toContainText('Bürowelt Krämer KG · je 100 Bl.')

  await page.getByRole('button', { name: 'Einzelpreis je Karton' }).click()
  await expect(page).toHaveURL(/preisart=einzel/)
  await expect(legende.getByRole('listitem')).toHaveCount(1)
  await expect(legende).toContainText('Bürowelt Krämer KG · Karton à 2.500 Bl.')

  await page.getByRole('table', { name: 'Käufe' }).getByRole('link', { name: '2026-48817' }).click()
  await expect(page).toHaveURL(/\/belege\/[0-9a-f-]+$/)
})
