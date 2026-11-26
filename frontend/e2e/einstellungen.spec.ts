import { expect, test } from '@playwright/test'

test.describe('Einstellungen Desktop', () => {
  test.skip(({ isMobile }) => isMobile, 'nur Desktop')

  test('/einstellungen leitet auf „Prüfungen“, der Eintrag ist aktiv', async ({ page }) => {
    await page.goto('/einstellungen')
    await expect(page).toHaveURL(/\/einstellungen\/pruefungen$/)
    await expect(page.getByRole('heading', { level: 2, name: 'Prüfungen & Schwellen' })).toBeVisible()
    const nav = page.getByRole('navigation', { name: 'Einstellungsbereiche' })
    await expect(nav.getByRole('link', { name: 'Prüfungen' })).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('switch')).toHaveCount(10)
  })

  test('USt-Status speichern: der Chip in der Navigation zeigt den neuen Status', async ({ page }) => {
    await page.goto('/einstellungen/pruefungen')
    const nav = page.getByRole('navigation', { name: 'Einstellungsbereiche' })
    await expect(nav.getByText('unbekannt')).toBeVisible()
    await nav.getByRole('link', { name: /^Umsatzsteuer/ }).click()
    await expect(page).toHaveURL(/\/einstellungen\/umsatzsteuer$/)
    await page.getByRole('radio', { name: /Kleinunternehmer/ }).check()
    await page.getByRole('button', { name: 'Speichern' }).click()
    await expect(page.getByText('Gespeichert')).toBeVisible()
    await expect(nav.getByText('Kleinunternehmer')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Speichern' })).toBeDisabled()
  })
})

test.describe('Einstellungen mobil', () => {
  test.skip(({ isMobile }) => !isMobile, 'nur mobil')

  test('zeigt statt der Navigation eine Auswahlliste über dem Inhalt', async ({ page }) => {
    await page.goto('/einstellungen/pruefungen')
    await expect(page.getByRole('navigation', { name: 'Einstellungsbereiche' })).toBeHidden()
    const auswahl = page.getByRole('combobox', { name: 'Bereich' })
    await expect(auswahl).toBeVisible()
    await auswahl.selectOption('aufbewahrung')
    await expect(page).toHaveURL(/\/einstellungen\/aufbewahrung$/)
    await expect(page.getByRole('heading', { level: 2, name: 'Aufbewahrung & Löschung' })).toBeVisible()
  })
})
