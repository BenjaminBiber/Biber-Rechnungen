import { expect, test } from '@playwright/test'

test('wartendes Konto freigeben: Balken verschwindet, Status „aktiv“', async ({ page }) => {
  await page.goto('/einstellungen/benutzer')
  const balken = page.getByRole('alert').filter({ hasText: 'Jonas Wagner' })
  await expect(balken).toBeVisible()
  await balken.getByRole('button', { name: 'Freigeben' }).click()
  await expect(balken).toBeHidden()
  const zeile = page.getByRole('row', { name: /j\.wagner@firma\.de/ })
  await expect(zeile.getByText('aktiv', { exact: true })).toBeVisible()
})

test('Mein Konto: Passwort-Dialog öffnet und schließt', async ({ page }) => {
  await page.goto('/einstellungen/konto')
  await page.getByRole('button', { name: 'Ändern' }).click()
  const dialog = page.getByRole('dialog', { name: 'Passwort ändern' })
  await expect(dialog).toBeVisible()
  await dialog.getByRole('button', { name: 'Abbrechen' }).click()
  await expect(dialog).toBeHidden()
})
