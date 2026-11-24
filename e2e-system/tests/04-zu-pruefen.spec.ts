import { expect, test } from '@playwright/test'
import { ADMIN, ADMIN_SITZUNG, belegLesen, hochladenUndWarten } from '../hilfen/system'

test.use({ storageState: ADMIN_SITZUNG })

type Befund = { pruefung: string; ergebnis: string; detail?: string | null }

// Abnahme Phase 1: Eine fehlerhafte Rechnung bekommt „zu prüfen“ mit Grund; jede Korrektur steht in der Historie.
test('fehlerhafter Scan → „zu prüfen“ mit Gründen → korrigieren → bestätigen → Historie', async ({ page }) => {
  const kategorieName = 'Büromaterial'
  const angelegt = await page.request.post('/api/kategorien', {
    data: { name: kategorieName, farbe: '#4F46E5', euerZeile: null, matching: { algorithmus: 'Keiner', muster: null, grossKlein: false } },
  })
  expect(angelegt.status(), await angelegt.text()).toBe(201)

  const vorgang = await hochladenUndWarten(page.request, 'scan-fehlerhaft.pdf')
  expect(vorgang.status).toBe('Fertig')
  const beleg = await belegLesen(page.request, vorgang.belegId!)
  const gruende = beleg.befunde.filter((b: Befund) => b.ergebnis === 'Fehlgeschlagen').map((b: Befund) => b.pruefung)
  expect(gruende.sort()).toEqual(['Konfidenz', 'Positionssumme'])

  // Sichtbar im Dashboard …
  await page.goto('/')
  const zuPruefen = page.getByRole('heading', { level: 2, name: /^Zu prüfen/ }).locator('xpath=ancestor::section[1]')
  await expect(zuPruefen.getByText('SCH-2026-0816', { exact: false }).or(zuPruefen.getByText('Bürobedarf Schulz GmbH')).first()).toBeVisible()

  // … und im Tab „Prüfung“.
  await page.goto(`/belege/${beleg.id}/pruefung`)
  await expect(page.getByText('Summe der Positionen = Netto')).toBeVisible()
  await expect(page.getByText(/Konfidenz 0,74 liegt unter der Schwelle/)).toBeVisible()

  // Fehlende Position ergänzen → Differenz 0.
  const tabs = page.getByRole('navigation', { name: 'Belegdaten' })
  await tabs.getByRole('link', { name: /^Positionen/ }).click()
  const vergleich = page.getByTestId('summenvergleich')
  await expect(vergleich).toContainText('Differenz 2,00')
  await page.getByRole('button', { name: '+ Position hinzufügen' }).click()
  await page.getByLabel('Text', { exact: true }).fill('Versandkostenpauschale')
  await page.getByLabel('Einzelpreis').fill('2,00')
  await page.getByRole('button', { name: 'Übernehmen' }).click()
  await page.getByRole('button', { name: 'Änderungen speichern' }).click()
  await expect(page.getByText('Positionen gespeichert')).toBeVisible()
  await expect(vergleich).toContainText('Differenz 0,00')

  // Kategorie ändern.
  await tabs.getByRole('link', { name: 'Daten' }).click()
  const daten = page.getByRole('form', { name: 'Daten' })
  await daten.getByLabel('Kategorie').click()
  await page.getByRole('listbox', { name: 'Kategorien' }).getByRole('option', { name: kategorieName }).click()
  await page.getByRole('button', { name: 'Änderungen speichern' }).click()
  await expect(page.getByText('Gespeichert')).toBeVisible()

  // Bestätigen (Konfidenz bleibt ein offener Befund) → Tag „zu prüfen“ weg.
  await page.getByRole('button', { name: 'Bestätigen' }).click()
  const dialog = page.getByRole('alertdialog')
  await expect(dialog).toBeVisible()
  await dialog.getByLabel('Begründung (optional, wird protokolliert)').fill('Gegen Original geprüft')
  await dialog.getByRole('button', { name: 'Trotzdem bestätigen' }).click()
  await expect(page.getByText('Beleg bestätigt')).toBeVisible()
  const kopf = page.getByRole('heading', { level: 1 }).locator('..')
  await expect(kopf.getByText('zu prüfen')).toHaveCount(0)

  // Jede Korrektur in der Historie: mit altem und neuem Wert und Benutzer.
  await tabs.getByRole('link', { name: 'Historie' }).click()
  await expect(page.getByText('Lückenloses Änderungsprotokoll · nicht änderbar oder löschbar')).toBeVisible()
  const eintraege = page.getByRole('listitem')
  const vonAdmin = eintraege.filter({ hasText: ADMIN.name })
  await expect(vonAdmin.filter({ hasText: /Position/ }).first()).toBeVisible()
  await expect(vonAdmin.filter({ hasText: /Kategorie/ }).filter({ hasText: kategorieName }).first()).toBeVisible()
  await expect(vonAdmin.filter({ hasText: 'Gegen Original geprüft' })).toBeVisible()
  await expect(vonAdmin.filter({ hasText: 'Tag entfernt: zu prüfen' })).toBeVisible()
  await expect(vonAdmin.filter({ hasText: 'Bestätigung ergänzt' })).toBeVisible()
  await expect(vonAdmin.filter({ hasText: 'Bestätigt von ergänzt' })).toContainText(ADMIN.name)
  // Zeitpunkte in Werten stehen in Ortszeit wie der Eintrag selbst, technische Feldnamen erscheinen nicht.
  const bestaetigung = vonAdmin.filter({ hasText: 'Bestätigung ergänzt' })
  const [, eintragZeit] = (await bestaetigung.innerText()).match(/(\d{2}:\d{2})/) ?? []
  await expect(bestaetigung).toContainText(new RegExp(`→\\s*\\d{2}\\.\\d{2}\\.\\d{4}, ${eintragZeit}`))
  await expect(eintraege.filter({ hasText: /BestaetigtVonId|KategorieManuell/ })).toHaveCount(0)
  await expect(vonAdmin.filter({ hasText: /Kategorie/ }).filter({ hasText: kategorieName }).locator('del')).toHaveCount(1)
  await expect(vonAdmin.filter({ hasText: 'Kategorie manuell gesetzt geändert' })).toContainText(/nein\s*→?\s*ja/)
})
