import { expect, test } from '@playwright/test'

test.describe('Einstellungen Eingang', () => {
  test('Document Intelligence: Monatslimit ändern und speichern', async ({ page }) => {
    await page.goto('/einstellungen/document-intelligence')
    await expect(page.getByRole('heading', { level: 2, name: 'Document Intelligence' })).toBeVisible()
    const limit = page.getByLabel('Monatslimit (Seiten)')
    await expect(limit).toHaveValue('500')
    await limit.fill('800')
    await page.getByRole('button', { name: 'Speichern' }).click()
    await expect(page.getByText('Gespeichert')).toBeVisible()
    await expect(page.getByText('412 / 800 Seiten', { exact: false })).toBeVisible()
  })

  test('E-Mail-Import: „Jetzt abrufen“ zeigt die Meldung', async ({ page }) => {
    await page.goto('/einstellungen/mail')
    await expect(page.getByRole('heading', { level: 2, name: 'E-Mail-Import' })).toBeVisible()
    await page.getByRole('button', { name: 'Jetzt abrufen' }).click()
    await expect(page.getByText('Postfach abgerufen – keine neuen Mails.')).toBeVisible()
  })
})
