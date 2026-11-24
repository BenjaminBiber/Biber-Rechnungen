import { expect, test } from '@playwright/test'

/** Beleg b4 „netzfunk Telekom · 7004 1182 3391“ aus den Mock-Fixtures, drei Seiten. */
const beleg = '/belege/b0000000-0000-4000-8000-000000000004'

test('Beleg aufteilen führt über den Dialog zu „Hochladen & Import“', async ({ page, isMobile }) => {
  if (isMobile) {
    // Die mobile Beleg-Ansicht (Design MobilBelegDetail) hat keinen Link „Aufteilen“ – der Dialog ist per Adresse erreichbar.
    await page.goto(`${beleg}/aufteilen`)
  } else {
    await page.goto(beleg)
    await page.getByRole('link', { name: 'Aufteilen' }).click()
  }
  const dialog = page.getByRole('dialog', { name: 'Beleg aufteilen' })
  await expect(dialog).toBeVisible()
  await expect(dialog.getByRole('img', { name: /^Seite \d$/ })).toHaveCount(3)
  await expect(dialog.getByRole('button', { name: 'In 1 Belege aufteilen' })).toBeDisabled()

  await dialog.getByRole('button', { name: 'Schnitt nach Seite 1' }).click()
  await expect(dialog.getByRole('list', { name: 'Teile' })).toContainText('A Seite 1 → neuer Beleg')
  await expect(dialog.getByRole('list', { name: 'Teile' })).toContainText('B Seiten 2–3 → neuer Beleg')
  await dialog.getByRole('button', { name: 'In 2 Belege aufteilen' }).click()

  await expect(page).toHaveURL(/\/hochladen$/)
  // Mobil (Design MobilUpload) heißt die Seite nur „Hochladen“.
  const ueberschrift = isMobile ? 'Hochladen' : 'Hochladen & Import'
  await expect(page.getByRole('heading', { level: 1, name: ueberschrift, exact: true })).toBeAttached()
  await expect(page.getByText('Beleg wird in 2 Belege aufgeteilt')).toBeVisible()
})
