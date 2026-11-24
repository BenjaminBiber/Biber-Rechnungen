import { expect, test } from '@playwright/test'

test.describe('Belege: Massenaktionen und Zeilenmenü', () => {
  test.skip(({ isMobile }) => isMobile, 'nur Desktop')

  test('zwei Belege bestätigen per Massenaktion', async ({ page }) => {
    await page.goto('/belege?ansicht=ZuPruefen')
    const zeilen = page.getByRole('table').getByRole('row')
    await expect(zeilen).toHaveCount(6)
    const erste = zeilen.nth(1)
    const zweite = zeilen.nth(2)
    const titel = [await erste.getByRole('link').innerText(), await zweite.getByRole('link').innerText()]
    await erste.getByRole('checkbox').check()
    await zweite.getByRole('checkbox').check()

    const leiste = page.getByRole('toolbar', { name: 'Massenaktionen' })
    await expect(leiste).toContainText('2 Belege ausgewählt')
    await leiste.getByRole('button', { name: 'Bestätigen' }).click()

    await expect(page.getByText('2 Belege bestätigt')).toBeVisible()
    await expect(leiste).toHaveCount(0)
    // Ohne „zu prüfen“ fallen die beiden aus der Ansicht.
    await expect(zeilen).toHaveCount(4)
    for (const t of titel) await expect(page.getByRole('table').getByRole('link', { name: t })).toHaveCount(0)
  })

  test('Zeilenmenü markiert einen Beleg als bezahlt', async ({ page }) => {
    // „Offen“ zeigt nur unbezahlte Belege; nach dem Markieren fällt der Beleg dort heraus.
    await page.goto('/belege?ansicht=Offen')
    const erste = page.getByRole('table').getByRole('row').nth(1)
    const titel = await erste.getByRole('link').innerText()
    await expect(erste.locator('.chip', { hasText: /^bezahlt$/ })).toHaveCount(0)

    await erste.getByRole('button', { name: /^Aktionen für/ }).click()
    await page.getByRole('menuitem', { name: /Als bezahlt markieren/ }).press('ArrowRight')
    await page.getByRole('menuitem', { name: 'bezahlt', exact: true }).click()
    await expect(page.getByText('Als „bezahlt“ markiert')).toBeVisible()

    // In der Ansicht „Alle“ trägt der Beleg den Chip „bezahlt“ (Wechsel in der App – der Mock-Zustand bleibt).
    await page.getByRole('button', { name: /^Ansicht/ }).click()
    await page.getByRole('menuitemradio', { name: /^Alle Belege/ }).click()
    const zeile = page.getByRole('table').getByRole('row').filter({ hasText: titel })
    await expect(zeile.locator('.chip', { hasText: /^bezahlt$/ })).toBeVisible()
  })
})
