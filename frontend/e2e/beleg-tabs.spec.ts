import { expect, test } from '@playwright/test'
import { belegTabOeffnen } from './hilfen/belegTabs'

/** Beleg b2 „Bürowelt Krämer KG · 2026-48817“ aus den Mock-Fixtures. */
const beleg = '/belege/b0000000-0000-4000-8000-000000000002'

test.describe('Beleg-Detail: Tabs', () => {
  test.beforeEach(async ({ page, isMobile }) => {
    await page.goto(beleg)
    await expect(page.getByRole('heading', { level: 1, name: 'Bürowelt Krämer KG' })).toBeVisible()
    if (isMobile) {
      // Mobil (Design MobilBelegDetail) gibt es keinen PDF-Viewer; die Tabs liegen hinter dem Aufklapp-Knopf.
      await page.getByRole('button', { name: 'Positionen, Prüfung, Historie und Dateien' }).click()
      await expect(page.getByRole('navigation', { name: 'Belegdaten' })).toBeVisible()
    } else {
      // Erst wenn die PDF-Seite steht, liegt alles an seinem Platz.
      await expect(page.getByRole('region', { name: 'Original' }).locator('canvas')).toBeVisible()
    }
  })

  test('alle fünf Tabs laden', async ({ page }) => {
    await belegTabOeffnen(page, /^Positionen/)
    await expect(page.getByRole('cell', { name: 'Kopierpapier A4 80 g, 5 × 500 Bl.' })).toBeVisible()

    await belegTabOeffnen(page, /^Prüfung/)
    await expect(page.getByText('Summe der Positionen = Netto')).toBeVisible()

    await belegTabOeffnen(page, 'Historie')
    await expect(page.getByText('Lückenloses Änderungsprotokoll · nicht änderbar oder löschbar')).toBeVisible()
    await expect(page.getByText('Kategorie geändert')).toBeVisible()

    await belegTabOeffnen(page, /^Dateien/)
    await expect(page.getByRole('article', { name: 'Original' })).toContainText('Prüfsumme ok')

    await belegTabOeffnen(page, 'Dubletten')
    await expect(page.getByText('Datei-Abgleich (SHA-256)')).toBeVisible()
  })

  test('fehlende Position ergänzen → Differenz 0,00 €', async ({ page }) => {
    await belegTabOeffnen(page, /^Positionen/)
    const vergleich = page.getByTestId('summenvergleich')
    await expect(vergleich).toContainText('Differenz 2,00')
    await page.getByRole('button', { name: '+ Position hinzufügen' }).click()
    await page.getByLabel('Text', { exact: true }).fill('Versandkostenpauschale')
    await page.getByLabel('Einzelpreis').fill('2,00')
    await page.getByRole('button', { name: 'Übernehmen' }).click()
    await page.getByRole('button', { name: 'Änderungen speichern' }).click()
    await expect(page.getByText('Positionen gespeichert')).toBeVisible()
    await expect(vergleich).toContainText('Differenz 0,00')
    await expect(vergleich).toHaveAttribute('data-abweichung', 'false')
  })
})
