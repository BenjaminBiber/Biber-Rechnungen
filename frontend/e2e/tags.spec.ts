import { expect, test } from '@playwright/test'

test('Zahlungs-Tag anlegen', async ({ page }) => {
  await page.goto('/tags')
  const zeilen = page.locator('table tbody tr')
  await expect(zeilen).toHaveCount(7)

  await page.getByRole('button', { name: 'Neuer Tag' }).click()
  const dialog = page.getByRole('dialog', { name: 'Neuer Tag' })
  await dialog.getByLabel('Name').fill('bezahlt Bar')
  await dialog.getByRole('radio', { name: 'Grün' }).click()
  await dialog.getByRole('radio', { name: /Zahlungs-Tag/ }).check()
  await dialog.getByRole('button', { name: 'Speichern' }).click()

  await expect(dialog).toBeHidden()
  await expect(zeilen).toHaveCount(8)
  await expect(zeilen.filter({ hasText: 'bezahlt Bar' })).toContainText('Zahlungs-Tag')
})

test('EÜR-Zeile einer Kategorie ändern', async ({ page }) => {
  await page.goto('/tags')
  await page.getByRole('tab', { name: /Kategorien/ }).click()
  await page.getByRole('button', { name: 'Sonstiges bearbeiten' }).click()

  const dialog = page.getByRole('dialog', { name: 'Kategorie bearbeiten' })
  await dialog.getByLabel('EÜR-Zeile (optional)').fill('Sonstige Betriebsausgaben')
  await dialog.getByRole('button', { name: 'Speichern' }).click()

  await expect(dialog).toBeHidden()
  await expect(page.locator('table tbody tr').filter({ hasText: 'Sonstiges' })).toContainText(
    'Sonstige Betriebsausgaben',
  )
})
