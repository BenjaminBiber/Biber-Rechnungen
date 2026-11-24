import { expect, test } from '@playwright/test'

test.describe('Einstellungen Betrieb', () => {
  test('API-Tokens: Token erstellen bis zur neuen Tabellenzeile', async ({ page }) => {
    await page.goto('/einstellungen/api-tokens')
    await expect(page.getByRole('heading', { level: 2, name: 'API-Tokens (MCP)' })).toBeVisible()
    await page.getByRole('button', { name: 'Token erstellen' }).click()
    await page.getByLabel('Name').fill('E2E-Token')
    await page.getByRole('button', { name: 'Erstellen' }).click()
    const dialog = page.getByRole('dialog', { name: 'Token „E2E-Token“ erstellt' })
    await expect(dialog.getByRole('textbox', { name: 'Token' })).toHaveValue(/^brk_/)
    await dialog.getByRole('button', { name: 'Fertig' }).click()
    await expect(dialog).toBeHidden()
    await expect(page.getByRole('row', { name: /E2E-Token/ })).toBeVisible()
  })

  test('Integrität & Sicherung lädt mit Tabelle', async ({ page }) => {
    await page.goto('/einstellungen/integritaet')
    await expect(page.getByRole('heading', { level: 2, name: 'Integrität & Sicherung' })).toBeVisible()
    await expect(page.getByRole('table')).toBeVisible()
    await expect(page.getByRole('columnheader', { name: 'Abweichungen' })).toBeVisible()
  })
})
