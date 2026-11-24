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
    // „Offen“ zeigt nur unbezahlte Belege – daraus einen wählen.
    await page.goto('/belege?ansicht=Offen')
    const erste = page.getByRole('table').getByRole('row').nth(1)
    const titel = await erste.getByRole('link').innerText()
    await expect(erste.locator('.chip', { hasText: /^bezahlt$/ })).toHaveCount(0)

    // Ganz per Tastatur: steht der Mauszeiger über dem aufklappenden Menü, kann ein Hover das Untermenü wieder schließen.
    await erste.getByRole('button', { name: /^Aktionen für/ }).press('Enter')
    await page.getByRole('menuitem', { name: /Als bezahlt markieren/ }).press('ArrowRight')
    // Ein Mausklick ins gerade aufklappende Untermenü scheiterte an „element is not stable“.
    const bezahlt = page.getByRole('menuitem', { name: 'bezahlt', exact: true })
    await expect(bezahlt).toBeFocused()
    await bezahlt.press('Enter')
    await expect(page.getByText('Als „bezahlt“ markiert')).toBeVisible()
    // Bezahlt gilt nicht mehr als „offen“: die Zeile fällt aus der Ansicht.
    await expect(page.getByRole('table').getByRole('link', { name: titel })).toHaveCount(0)
  })
})
