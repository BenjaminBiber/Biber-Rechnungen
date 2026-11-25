import { expect, test } from '@playwright/test'
import { belegTabOeffnen } from './hilfen/belegTabs'

/** Beleg b2 „Bürowelt Krämer KG · 2026-48817“ aus den Mock-Fixtures. */
const beleg = '/belege/b0000000-0000-4000-8000-000000000002'

test.describe('Beleg-Detail', () => {
  test.skip(({ isMobile }) => isMobile, 'mobil: e2e/mobil.spec.ts')
  test.beforeEach(async ({ page }) => {
    await page.goto(beleg)
    await expect(page.getByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' })).toBeVisible()
    // Erst wenn die PDF-Seite steht, liegt alles an seinem Platz (mobil steht der Viewer über dem Formular).
    await expect(page.getByRole('region', { name: 'Original' }).locator('canvas')).toBeVisible()
  })

  test('Korrektur speichern', async ({ page }) => {
    const daten = page.getByRole('form', { name: 'Daten' })
    await daten.getByLabel('Kategorie').click()
    await page.getByRole('option', { name: /Hardware/ }).click()
    await expect(daten.getByLabel('Kategorie')).toHaveText(/Hardware/)
    await page.getByRole('button', { name: 'Änderungen speichern' }).click()
    await expect(page.getByText('Gespeichert')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Änderungen speichern' })).toBeDisabled()
  })

  test('Bestätigen trotz Befund', async ({ page }) => {
    // Auf Laptop-Breiten stehen die Tags nur im Formular, nicht im Kopf.
    const daten = page.getByRole('form', { name: 'Daten' })
    await expect(daten.getByText('zu prüfen')).toBeVisible()
    await page.getByRole('button', { name: 'Bestätigen' }).click()
    const dialog = page.getByRole('alertdialog', { name: 'Beleg mit 2 offenen Befunden bestätigen?' })
    await expect(dialog).toBeVisible()
    await expect(dialog.getByRole('listitem')).toHaveCount(2)
    await dialog.getByLabel('Begründung (optional, wird protokolliert)').fill('Gegen Original geprüft')
    await dialog.getByRole('button', { name: 'Trotzdem bestätigen' }).click()
    await expect(page.getByText('Beleg bestätigt')).toBeVisible()
    await expect(daten.getByText('zu prüfen')).toHaveCount(0)
  })

  test('Tab-Links wechseln die URL', async ({ page }) => {
    for (const [name, pfad] of [
      [/^Positionen/, 'positionen'],
      [/^Prüfung/, 'pruefung'],
      [/^Historie/, 'historie'],
      [/^Dateien/, 'dateien'],
      [/^Dubletten/, 'dubletten'],
    ] as const) {
      await belegTabOeffnen(page, name)
      await expect(page).toHaveURL(new RegExp(`${beleg}/${pfad}$`))
    }
    await belegTabOeffnen(page, 'Daten')
    await expect(page).toHaveURL(new RegExp(`${beleg}$`))
    await expect(page.getByRole('form', { name: 'Daten' })).toBeVisible()
  })

  test('PDF-Seite wird gerendert', async ({ page }) => {
    const viewer = page.getByRole('region', { name: 'Original' })
    const canvas = viewer.locator('canvas')
    await expect(canvas).toBeVisible()
    const groesse = await canvas.boundingBox()
    expect(groesse?.width ?? 0).toBeGreaterThan(100)
    await expect(viewer.getByText('Seite 1 / 1')).toBeVisible()
  })

  test('hat keine horizontale Bildlaufleiste', async ({ page }) => {
    const ueberstand = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(ueberstand).toBeLessThanOrEqual(0)
  })
})
