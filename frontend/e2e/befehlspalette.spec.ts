import { expect, test } from '@playwright/test'

test.describe('Befehlspalette', () => {
  test('Strg K → „toner“ → Enter öffnet den Beleg', async ({ page }) => {
    await page.goto('/tags')
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    await page.keyboard.press('Control+k')
    const palette = page.getByRole('dialog', { name: 'Befehlspalette' })
    await expect(palette).toBeVisible()

    await palette.getByRole('combobox').fill('toner')
    const ersterBeleg = palette.getByRole('group', { name: 'Belege' }).getByRole('option').first()
    await expect(ersterBeleg).toContainText('Bürowelt Krämer KG')
    await page.keyboard.press('Enter')

    await expect(page).toHaveURL(/\/belege\/[^/?]+$/)
    await expect(palette).toBeHidden()
  })

  test('Klick ins Suchfeld der Kopfleiste öffnet die Palette', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: 'Volltextsuche' }).click()
    await expect(page.getByRole('dialog', { name: 'Befehlspalette' })).toBeVisible()
    await expect(page.getByRole('option', { name: /Zu prüfen öffnen/ })).toBeVisible()
  })
})
