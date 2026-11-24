import { expect, test } from '@playwright/test'
import AdmZip from 'adm-zip'
import { readFileSync } from 'node:fs'
import { ADMIN_SITZUNG } from '../hilfen/system'
import { ablageDateien, ablageLesen, sha256 } from '../hilfen/werkzeug'
import { xlsxLesen, type Zelle } from '../hilfen/xlsx'

test.use({ storageState: ADMIN_SITZUNG })

function zahl(wert: Zelle): number | null {
  return typeof wert === 'number' ? wert : null
}

test('Export des Zeitraums: xlsx mit Belegliste, Summen und Vorsteuer, ZIP der Originale mit Prüfsummen', async ({ page }) => {
  const zeitraum = { von: '2026-01-01', bis: '2026-12-31' }
  const vorschauAnfrage = {
    ...zeitraum,
    bezeichnung: 'E2E',
    belegliste: true,
    beleglisteFormat: 'xlsx',
    summenJeKategorie: true,
    vorsteuer: true,
    zipOriginale: true,
    kategorieIds: [],
    korrespondentIds: [],
    tagIds: [],
    dublettenAusschliessen: true,
    zuPruefenAusschliessen: false,
  }
  const vorschau = (await (await page.request.post('/api/export/vorschau', { data: vorschauAnfrage })).json()) as {
    anzahlBelege: number
    vorsteuer: number | null
  }
  expect(vorschau.anzahlBelege).toBeGreaterThanOrEqual(5)
  expect(vorschau.vorsteuer).not.toBeNull()

  await page.goto('/export')
  await page.getByLabel('Von').fill('01.01.2026')
  await page.getByLabel('Bis').fill('31.12.2026')
  await page.getByRole('checkbox', { name: 'ZIP der Originale' }).check()
  await expect(page.getByRole('checkbox', { name: 'Belegliste' })).toBeChecked()
  await expect(page.getByRole('checkbox', { name: 'Summen je Kategorie' })).toBeChecked()
  await expect(page.getByRole('checkbox', { name: 'Vorsteuer je UStVA-Zeitraum' })).toBeChecked()
  const vorschauKarte = page.getByRole('complementary')
  await expect(vorschauKarte.getByText(String(vorschau.anzahlBelege), { exact: true }).first()).toBeVisible()
  await expect(vorschauKarte.getByRole('row').filter({ hasText: 'Werkzeug' })).toContainText('Sonstige Betriebsausgaben')

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Export erstellen' }).click()
  const datei = await download
  expect(datei.suggestedFilename()).toMatch(/\.zip$/)
  const zip = new AdmZip(readFileSync((await datei.path())!))
  const namen = zip.getEntries().map((e) => e.entryName)
  expect(namen).toContain('export.xlsx')
  expect(namen).toContain('pruefsummen.sha256')

  // Originale: genau die Dateien der Ablage, byte-gleich und mit stimmiger Prüfsummenliste.
  // Je Beleg mindestens ein Original; ZUGFeRD bringt PDF und eingebettetes XML mit.
  const originale = namen.filter((n) => n.startsWith('originale/'))
  expect(originale.length).toBeGreaterThanOrEqual(vorschau.anzahlBelege)
  expect(originale.filter((n) => n.includes('_ZF-2026-0004.'))).toHaveLength(2)
  const ablage = new Set(ablageDateien())
  const pruefsummen = zip.readAsText('pruefsummen.sha256').trim().split('\n')
  expect(pruefsummen).toHaveLength(originale.length)
  for (const name of originale) {
    const pfad = name.slice('originale/'.length)
    expect(ablage.has(pfad), pfad).toBeTruthy()
    const inhalt = zip.readFile(name)!
    expect(sha256(inhalt)).toBe(sha256(ablageLesen(pfad)))
    expect(pruefsummen).toContain(`${sha256(inhalt)}  ${name}`)
  }

  const mappe = xlsxLesen(zip.readFile('export.xlsx')!)
  expect([...mappe.keys()]).toEqual(expect.arrayContaining(['Belegliste', 'Summen je Kategorie', 'Vorsteuer']))

  // Belegliste: alle Belege, Summenzeile, kein Zahlungsdatum.
  const liste = mappe.get('Belegliste')!
  const kopf = liste.findIndex((z) => z.includes('Rechnungsdatum'))
  expect(kopf).toBeGreaterThanOrEqual(0)
  const spalten = liste[kopf].map(String)
  expect(spalten).toEqual(expect.arrayContaining(['Korrespondent', 'Rechnungsnummer', 'Kategorie', 'EÜR-Zeile', 'Brutto', 'SHA-256']))
  expect(spalten.some((s) => /zahlung/i.test(s))).toBe(false)
  const belegZeilen = liste.slice(kopf + 1).filter((z) => z[0] !== 'Summe (EUR)' && z.some((w) => w != null))
  expect(belegZeilen).toHaveLength(vorschau.anzahlBelege)
  const nummern = belegZeilen.map((z) => z[spalten.indexOf('Rechnungsnummer')])
  expect(nummern).toEqual(expect.arrayContaining(['SCH-2026-0815', 'SCH-2026-0816', 'ZF-2026-0004', '123456XX']))
  for (const nummer of nummern) expect(originale.some((n) => n.includes(`_${nummer}.`)), String(nummer)).toBe(true)
  expect(liste.some((z) => z[0] === 'Summe (EUR)')).toBe(true)

  // Summen je Kategorie mit EÜR-Zeile.
  const summen = mappe.get('Summen je Kategorie')!
  const werkzeug = summen.find((z) => z.includes('Werkzeug'))
  expect(werkzeug).toContain('Sonstige Betriebsausgaben')

  // Vorsteuer: Gesamtsumme wie in der Vorschau.
  const vorsteuer = mappe.get('Vorsteuer')!
  const werte = vorsteuer.flat().map(zahl).filter((w): w is number => w !== null)
  expect(werte.some((w) => Math.abs(w - vorschau.vorsteuer!) < 0.005)).toBe(true)
})
