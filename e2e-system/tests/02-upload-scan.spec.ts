import { expect, test } from '@playwright/test'
import { ADMIN_SITZUNG, belegLesen, testdatei } from '../hilfen/system'
import { ablageDateien, ablageLesen, sha256 } from '../hilfen/werkzeug'
import { belegTabOeffnen } from '../hilfen/belegTabs'

test.use({ storageState: ADMIN_SITZUNG })

// Abnahme Phase 1: Eine gescannte PDF-Rechnung landet korrekt ausgelesen in der Ablage.
test('Scan hochladen → „Fertig“ → Beleg mit Kopfdaten und Positionen → Datei in der Ablage', async ({ page }) => {
  const inhalt = testdatei('scan-stimmig.pdf')

  await page.goto('/hochladen')
  await page.getByLabel('Dateien auswählen').setInputFiles({ name: 'scan-stimmig.pdf', mimeType: 'application/pdf', buffer: inhalt })
  const liste = page.getByRole('region', { name: 'Hochgeladene Dateien' })
  await expect(liste.getByText('scan-stimmig.pdf')).toBeVisible()
  await expect(liste.getByText('Fertig', { exact: true })).toBeVisible({ timeout: 90_000 })
  await liste.getByRole('link', { name: 'Beleg öffnen' }).click()

  // Kopfdaten aus Document Intelligence.
  await expect(page.getByRole('heading', { level: 1, name: 'Bürobedarf Schulz GmbH' })).toBeVisible()
  const daten = page.getByRole('form', { name: 'Daten' })
  await expect(daten.getByLabel('Rechnungsnummer')).toHaveValue('SCH-2026-0815')
  const belegId = page.url().match(/belege\/([0-9a-f-]{36})/)![1]
  const beleg = await belegLesen(page.request, belegId)
  expect(beleg).toMatchObject({
    rechnungsnummer: 'SCH-2026-0815',
    rechnungsdatum: '2026-10-15',
    netto: 100,
    steuer: 19,
    brutto: 119,
  })
  expect(beleg.positionen).toHaveLength(2)
  expect(beleg.positionen.map((p: { text: string }) => p.text)).toEqual(['Druckerpapier A4', 'Toner schwarz'])

  // Tag „Inbox“; „zu prüfen“ nur wegen des neuen Korrespondenten.
  // Auf Laptop-Breiten stehen die Tags nur im Formular, nicht im Kopf.
  const tags = daten.getByText('Tags', { exact: true }).locator('..')
  await expect(tags.getByText('Inbox')).toBeVisible()
  const fehlgeschlagen = beleg.befunde.filter((b: { ergebnis: string }) => b.ergebnis === 'Fehlgeschlagen')
  expect(fehlgeschlagen.map((b: { pruefung: string }) => b.pruefung)).toEqual(['NeuerKorrespondent'])

  await belegTabOeffnen(page, /^Positionen/)
  await expect(page.getByRole('cell', { name: 'Druckerpapier A4', exact: true }).first()).toBeVisible()
  await expect(page.getByRole('cell', { name: 'Toner schwarz', exact: true }).first()).toBeVisible()
  await expect(page.getByTestId('summenvergleich')).toContainText('Differenz 0,00')

  // Neuen Korrespondenten bestätigen → „zu prüfen“ verschwindet, der Korrespondent gilt als geprüft.
  await belegTabOeffnen(page, 'Daten')
  await expect(tags.getByText('zu prüfen')).toBeVisible()
  await page.getByRole('button', { name: 'Bestätigen', exact: true }).click()
  const dialog = page.getByRole('alertdialog', { name: 'Beleg mit 1 offenen Befund bestätigen?' })
  await expect(dialog.getByText('Neuer Korrespondent')).toBeVisible()
  await dialog.getByRole('button', { name: 'Trotzdem bestätigen' }).click()
  await expect(page.getByText('Beleg bestätigt')).toBeVisible()
  await expect(tags.getByText('zu prüfen')).toHaveCount(0)
  const bestaetigt = await belegLesen(page.request, belegId)
  expect(bestaetigt.tags.map((t: { name: string }) => t.name)).not.toContain('zu prüfen')

  // Ablage: <Jahr>/<Korrespondent>/<Datum>_<Nr>.pdf, Inhalt byte-gleich.
  const pfad = '2026/Buerobedarf_Schulz_GmbH/2026-10-15_SCH-2026-0815.pdf'
  expect(ablageDateien()).toContain(pfad)
  expect(sha256(ablageLesen(pfad))).toBe(sha256(inhalt))
})
