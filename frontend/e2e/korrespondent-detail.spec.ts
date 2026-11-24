import { expect, test } from '@playwright/test'
import { korrespondentIds } from '../src/mocks/fixtures/ids'

test('Korrespondent-Detail: alle Karten und Logo hochladen', async ({ page }) => {
  await page.goto(`/korrespondenten/${korrespondentIds.bueroweltKraemer}?jahr=2026`)
  await expect(page.getByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' })).toBeVisible()
  await expect(page.getByRole('group', { name: 'Kennzahlen' })).toContainText('2.223,00')
  for (const karte of ['Stammdaten', 'Zuordnung', 'Kostenverlauf 2026', 'Belege', 'Artikel dieses Korrespondenten'])
    await expect(page.getByRole('heading', { level: 2, name: new RegExp(`^${karte}`) })).toBeVisible()

  await page.getByRole('button', { name: 'Logo ändern' }).click()
  const dialog = page.getByRole('dialog', { name: 'Logo festlegen' })
  await dialog.getByRole('tab', { name: 'Datei hochladen' }).click()
  await dialog.getByLabel(/Logo-Datei/).setInputFiles({
    name: 'logo.png',
    mimeType: 'image/png',
    buffer: Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=',
      'base64',
    ),
  })
  await dialog.getByRole('button', { name: 'Logo speichern' }).click()
  await expect(page.getByText('Logo hochgeladen')).toBeVisible()
  await expect(dialog).toBeHidden()
})
