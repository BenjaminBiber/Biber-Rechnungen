import { expect, test } from '@playwright/test'

test('hochgeladene Datei endet als „Fertig“ mit Link zum Beleg', async ({ page }) => {
  await page.goto('/hochladen')
  await page.getByLabel('Dateien auswählen').setInputFiles({
    name: 'Rechnung_E2E.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('%PDF-1.4 E2E'),
  })
  const liste = page.getByRole('region', { name: 'Hochgeladene Dateien' })
  await expect(liste.getByText('Rechnung_E2E.pdf')).toBeVisible()
  await expect(liste.getByText('Fertig', { exact: true })).toBeVisible({ timeout: 20_000 })
  await expect(liste.getByRole('link', { name: 'Beleg öffnen' })).toHaveAttribute('href', /^\/belege\//)
})

test('Ziehen hebt die Dropzone hervor', async ({ page }) => {
  await page.goto('/hochladen')
  const zone = page.getByTestId('dropzone')
  await expect(zone).not.toHaveAttribute('data-ziehen')
  const transfer = await page.evaluateHandle(() => new DataTransfer())
  await zone.dispatchEvent('dragenter', { dataTransfer: transfer })
  await expect(zone).toHaveAttribute('data-ziehen', 'true')
})

test('Tabelle Import-Vorgänge ist sichtbar und filterbar', async ({ page }) => {
  await page.goto('/hochladen')
  const tabelle = page.getByRole('table')
  await page.getByRole('button', { name: 'Letzte 30 Tage' }).click()
  await expect(tabelle.getByRole('row')).toHaveCount(8)
  await page.getByRole('button', { name: 'MCP' }).click()
  await expect(tabelle.getByRole('row')).toHaveCount(2)
  await expect(tabelle.getByText('hardware_rechnung.pdf')).toBeVisible()
})
