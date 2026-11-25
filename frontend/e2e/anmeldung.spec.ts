import { expect, test, type Page } from '@playwright/test'

/** Startzustand des Mock-Servers (siehe src/mocks/browser.ts). */
async function mockSzenario(page: Page, szenario: 'abgemeldet' | 'nichtEingerichtet') {
  await page.addInitScript((s) => localStorage.setItem('mock-szenario', s), szenario)
}

test('abgemeldet: Anmelden mit TOTP führt zum Dashboard', async ({ page }) => {
  await mockSzenario(page, 'abgemeldet')
  await page.goto('/belege')
  await expect(page).toHaveURL(/\/anmelden/)
  await page.getByLabel('E-Mail-Adresse').fill('m.keller@firma.de')
  await page.getByLabel('Passwort', { exact: true }).fill('biber-demo')
  await page.getByRole('button', { name: 'Anmelden' }).click()

  await expect(page.getByRole('heading', { name: 'Bestätigungscode' })).toBeVisible()
  await page.getByLabel('Ziffer 1').click()
  await page.keyboard.type('123456')
  await page.getByRole('button', { name: 'Anmelden' }).click()

  await expect(page).toHaveURL(/^http:\/\/[^/]+\/$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard', exact: true })).toBeVisible()
})

test('nicht eingerichtet: Ersteinrichtung wird erzwungen', async ({ page }) => {
  await mockSzenario(page, 'nichtEingerichtet')
  await page.goto('/anmelden')
  await expect(page).toHaveURL(/schritt=ersteinrichtung/)
  await expect(page.getByRole('heading', { name: 'Admin-Konto anlegen' })).toBeVisible()
  await expect(page.getByRole('img', { name: 'QR-Code für die Authenticator-App' })).toBeVisible()
  await expect(page.getByText('JBSW Y3DP EHPK 3PXP')).toBeVisible()
})

test('angemeldet: /anmelden leitet nach /', async ({ page }) => {
  await page.goto('/anmelden')
  await expect(page).toHaveURL(/^http:\/\/[^/]+\/$/)
})

test('Bürofoto mit Logo neben dem Formular, mobil als Band darüber', async ({ page, isMobile }) => {
  await mockSzenario(page, 'abgemeldet')
  await page.goto('/anmelden')
  await expect(page.getByRole('tab', { name: 'Anmelden', selected: true })).toBeVisible()
  const foto = page.getByTestId('anmelde-feld')
  await expect(foto).toBeVisible()
  const fotoBox = await foto.boundingBox()
  const formularBox = await page.getByLabel('E-Mail-Adresse').boundingBox()
  if (isMobile) expect(fotoBox!.y + fotoBox!.height).toBeLessThanOrEqual(formularBox!.y)
  else expect(fotoBox!.x + fotoBox!.width).toBeLessThanOrEqual(formularBox!.x)
})
