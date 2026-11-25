import { expect, test } from '@playwright/test'
import { ADMIN, sitzungSpeichern, zugangSpeichern } from '../hilfen/system'
import { frischerTotpCode, totpCode } from '../hilfen/totp'

test('leeres System: Ersteinrichtung mit QR-Schlüssel, Abmelden, Anmelden mit Passwort und TOTP', async ({ page }) => {
  await page.goto('/')
  await expect(page).toHaveURL(/schritt=ersteinrichtung/)
  await expect(page.getByRole('heading', { name: 'Admin-Konto anlegen' })).toBeVisible()
  await expect(page.getByRole('img', { name: 'QR-Code für die Authenticator-App' })).toBeVisible()
  const schluessel = (await page.getByTestId('totp-schluessel').innerText()).replace(/\s/g, '')
  expect(schluessel).toMatch(/^[A-Z2-7]{16,}$/)
  expect(totpCode(schluessel)).toMatch(/^\d{6}$/)

  await page.getByLabel('Name').fill(ADMIN.name)
  await page.getByLabel('E-Mail-Adresse').fill(ADMIN.email)
  await page.getByLabel('Passwort', { exact: true }).fill(ADMIN.passwort)
  await page.getByRole('button', { name: 'Konto anlegen und fortfahren' }).click()
  zugangSpeichern({ ...ADMIN, totpSchluessel: schluessel })

  await expect(page).toHaveURL(/^http:\/\/[^/]+\/$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard', exact: true })).toBeVisible()

  // Abmelden über das Benutzermenü.
  await page.getByRole('button', { name: 'Benutzermenü' }).click()
  await page.getByRole('button', { name: 'Abmelden' }).click()
  await expect(page).toHaveURL(/\/anmelden/)

  // Anmelden mit Passwort und Code aus der „Authenticator-App“.
  await page.getByLabel('E-Mail-Adresse').fill(ADMIN.email)
  await page.getByLabel('Passwort', { exact: true }).fill(ADMIN.passwort)
  await page.getByRole('button', { name: 'Anmelden' }).click()
  await expect(page.getByRole('heading', { name: 'Bestätigungscode' })).toBeVisible()
  const code = await frischerTotpCode(schluessel)
  await page.getByLabel('Ziffer 1').click()
  await page.keyboard.type(code)
  await page.getByRole('button', { name: 'Anmelden' }).click()

  await expect(page).toHaveURL(/^http:\/\/[^/]+\/$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard', exact: true })).toBeVisible()
  await sitzungSpeichern(page)
})
