import { expect, test } from '@playwright/test'

test.describe('Belege', () => {
  test.skip(({ isMobile }) => isMobile, 'nur Desktop')

  test('wechselt Tabelle → kleine Karten → große Karten', async ({ page }) => {
    await page.goto('/belege')
    await expect(page.getByRole('heading', { level: 1, name: 'Alle Belege' })).toBeVisible()
    await expect(page.getByRole('table').getByRole('row')).toHaveCount(13)

    const darstellung = page.getByRole('radiogroup', { name: 'Darstellung' })
    await darstellung.getByRole('radio', { name: 'Kleine Karten' }).click()
    await expect(page).toHaveURL(/darstellung=klein/)
    await expect(page.getByRole('article')).toHaveCount(12)
    await expect(page.getByRole('table')).toHaveCount(0)

    await darstellung.getByRole('radio', { name: 'Große Karten' }).click()
    await expect(page).toHaveURL(/darstellung=gross/)
    await expect(page.getByRole('button', { name: 'Ähnliche' })).toHaveCount(12)

    await darstellung.getByRole('radio', { name: 'Tabelle' }).click()
    await expect(page.getByRole('table')).toBeVisible()
  })

  test('Ansicht „Zu prüfen“ aus der URL', async ({ page }) => {
    await page.goto('/belege?ansicht=ZuPruefen')
    await expect(page.getByRole('heading', { level: 1, name: 'Zu prüfen' })).toBeVisible()
    await expect(page).toHaveURL(/ansicht=ZuPruefen/)
    await expect(page.getByRole('table').getByRole('row')).toHaveCount(6)
    await expect(page.getByText('5 Belege · Seite 1 von 1')).toBeVisible()
  })

  test('Klick auf einen Titel öffnet das Detail', async ({ page }) => {
    await page.goto('/belege')
    await page.getByRole('link', { name: 'Büromaterial Bestellung' }).click()
    await expect(page).toHaveURL(/\/belege\/[0-9a-f-]+$/)
    await expect(page.getByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' })).toBeVisible()
  })

  test('URL mit Filtern stellt den Zustand wieder her', async ({ page }) => {
    await page.goto('/belege?darstellung=gross&zahlstatus=Ueberfaellig&sortierung=Brutto')
    await expect(page.getByRole('radio', { name: 'Große Karten' })).toHaveAttribute('aria-checked', 'true')
    await expect(page.getByRole('article')).toHaveCount(2)
    await expect(page.getByRole('article').first()).toContainText('Jahresabrechnung Strom')
    await expect(page.getByRole('button', { name: 'Zahlstatus: Überfällig' })).toBeVisible()
    await expect(page.getByRole('button', { name: /^Sortierung: Brutto, absteigend/ })).toBeVisible()

    await page.getByRole('button', { name: 'Filter zurücksetzen' }).click()
    await expect(page.getByRole('article')).toHaveCount(12)
    await expect(page).not.toHaveURL(/zahlstatus/)
  })
})
