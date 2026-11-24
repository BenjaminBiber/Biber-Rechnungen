import { expect, test, type Page } from '@playwright/test'
import { analyseAufrufe } from '../hilfen/azure'
import { ADMIN_SITZUNG, belegLesen, hochladenUndWarten, testdatei } from '../hilfen/system'
import { ablageDateien, ablageLesen, sha256 } from '../hilfen/werkzeug'

test.use({ storageState: ADMIN_SITZUNG })

type Befund = { pruefung: string; ergebnis: string; detail?: string | null }

async function erkannteFelder(page: Page, belegId: string) {
  await page.goto(`/belege/${belegId}`)
  const original = page.getByRole('region', { name: 'Original' })
  await original.getByRole('button', { name: 'Erkannte Felder' }).click()
  return original.getByRole('table', { name: 'Erkannte Felder' })
}

// Abnahme Phase 1: ZUGFeRD und XRechnung werden aus dem XML gelesen – ohne Document Intelligence.
test('ZUGFeRD und XRechnung: Daten aus dem XML, azure-mock nicht aufgerufen, KoSIT ok, Visualisierung', async ({ page }) => {
  const aufrufeVorher = await analyseAufrufe()

  const zugferd = await hochladenUndWarten(page.request, 'zugferd.pdf')
  expect(zugferd.status).toBe('Fertig')
  const xrechnung = await hochladenUndWarten(page.request, 'xrechnung.xml')
  expect(xrechnung.status).toBe('Fertig')

  expect(await analyseAufrufe()).toBe(aufrufeVorher)

  const zf = await belegLesen(page.request, zugferd.belegId!)
  expect(zf.extraktion.quelle).toBe('ZUGFeRD')
  expect(zf.rechnungsnummer).toBe('ZF-2026-0004')
  expect(zf.positionen.map((p: { text: string }) => p.text)).toEqual([expect.stringMatching(/^Kabel/), expect.stringMatching(/^Schrauben/)])
  expect(zf.befunde.find((b: Befund) => b.pruefung === 'Kosit')?.ergebnis).not.toBe('Fehlgeschlagen')

  const xr = await belegLesen(page.request, xrechnung.belegId!)
  expect(xr.extraktion.quelle).toBe('XRechnung')
  expect(xr.rechnungsnummer).toBe('123456XX')
  expect(xr.befunde.find((b: Befund) => b.pruefung === 'Kosit')?.ergebnis).toBe('Ok')

  // Quelle sichtbar im Beleg.
  const felderZf = await erkannteFelder(page, zf.id)
  await expect(felderZf.getByRole('row', { name: /Rechnungsnummer ZF-2026-0004 ZUGFeRD/ })).toBeVisible()
  const felderXr = await erkannteFelder(page, xr.id)
  await expect(felderXr.getByRole('row', { name: /Rechnungsnummer 123456XX XRechnung/ })).toBeVisible()

  // Visualisierung der XRechnung im Viewer.
  const original = page.getByRole('region', { name: 'Original' })
  await original.getByRole('button', { name: 'Visualisierung' }).click()
  const visualisierung = original.frameLocator('iframe[title="Visualisierung der E-Rechnung"]')
  await expect(visualisierung.getByText('123456XX').first()).toBeVisible()

  // Beide Originale liegen byte-gleich in der Ablage.
  const ablage = ablageDateien()
  const zfPfad = ablage.find((p) => p.endsWith('_ZF-2026-0004.pdf'))
  const xrPfad = ablage.find((p) => p.endsWith('_123456XX.xml'))
  expect(zfPfad, ablage.join('\n')).toBeTruthy()
  expect(xrPfad, ablage.join('\n')).toBeTruthy()
  expect(sha256(ablageLesen(zfPfad!))).toBe(sha256(testdatei('zugferd.pdf')))
  expect(sha256(ablageLesen(xrPfad!))).toBe(sha256(testdatei('xrechnung.xml')))
})

test('XRechnung ohne Käuferreferenz → „zu prüfen“ mit BR-DE-15', async ({ page }) => {
  const vorgang = await hochladenUndWarten(page.request, 'xrechnung-ohne-kaeuferreferenz.xml')
  expect(vorgang.status).toBe('Fertig')
  const beleg = await belegLesen(page.request, vorgang.belegId!)
  const kosit = beleg.befunde.find((b: Befund) => b.pruefung === 'Kosit')
  expect(kosit?.ergebnis).toBe('Fehlgeschlagen')
  expect(kosit?.detail).toContain('BR-DE-15')
  expect(beleg.tags.map((t: { name: string }) => t.name)).toContain('zu prüfen')

  await page.goto(`/belege/${beleg.id}/pruefung`)
  await expect(page.getByText(/BR-DE-15/).first()).toBeVisible()
})
