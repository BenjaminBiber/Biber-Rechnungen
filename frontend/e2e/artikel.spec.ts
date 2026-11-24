import { expect, test } from '@playwright/test'

test.describe('Artikel', () => {
  test.skip(({ isMobile }) => isMobile, 'nur Desktop')

  test('bildet aus zwei Artikeln einen Stammartikel', async ({ page }) => {
    await page.goto('/artikel')
    await expect(page.getByRole('heading', { level: 1, name: 'Artikel' })).toBeVisible()
    await expect(page.getByRole('table').getByRole('row')).toHaveCount(10)

    await page.getByRole('checkbox', { name: 'Ordner breit 8 cm, blau auswählen' }).check()
    await page.getByRole('checkbox', { name: 'Kugelschreiber blau auswählen' }).check()
    await page.getByRole('button', { name: '2 zu Stammartikel zusammenführen' }).click()

    const dialog = page.getByRole('dialog', { name: 'Stammartikel bilden' })
    await dialog.getByRole('textbox', { name: 'Name' }).fill('Bürobedarf klein')
    await dialog.getByRole('combobox', { name: 'Basiseinheit' }).selectOption('Stück')
    await dialog.getByRole('textbox', { name: 'Faktor Ordner breit 8 cm, blau' }).fill('1')
    await dialog.getByRole('textbox', { name: 'Faktor Kugelschreiber blau' }).fill('1')
    await expect(dialog.getByRole('status')).toContainText('Grundpreis je 1 Stück: Bürowelt 2,49')
    await dialog.getByRole('button', { name: 'Stammartikel anlegen' }).click()

    await expect(page).toHaveURL(/\/artikel\/stammartikel\/[0-9a-f-]+$/)
  })

  test('Reiter „Stammartikel“ zeigt Karten', async ({ page }) => {
    await page.goto('/artikel')
    await page.getByRole('tab', { name: /Stammartikel/ }).click()
    await expect(page).toHaveURL(/reiter=stammartikel/)
    await expect(page.getByRole('article')).toHaveCount(4)
  })
})
