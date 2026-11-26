import { expect, test } from '@playwright/test'
import { PDFDocument } from 'pdf-lib'
import { ADMIN_SITZUNG, aufVorgaengeWarten, belegLesen, hochladenUndWarten, testdatei, vorgaenge } from '../hilfen/system'
import { ablageDateien, ablageLesen, sha256 } from '../hilfen/werkzeug'

test.use({ storageState: ADMIN_SITZUNG })

async function anzahlBelege(request: import('@playwright/test').APIRequestContext): Promise<number> {
  return ((await (await request.get('/api/belege/zaehler')).json()) as { alle: number }).alle
}

test('dieselbe Datei erneut → Vorgang „Dublette“, nicht doppelt gezählt', async ({ page }) => {
  const vorher = await anzahlBelege(page.request)
  const vorgang = await hochladenUndWarten(page.request, 'scan-stimmig.pdf')
  expect(vorgang.status).toBe('Dublette')
  expect(vorgang.duplikatVonBelegId).toBeTruthy()
  const original = await belegLesen(page.request, vorgang.duplikatVonBelegId!)
  expect(original.rechnungsnummer).toBe('SCH-2026-0815')
  expect(await anzahlBelege(page.request)).toBe(vorher)

  await page.goto('/hochladen')
  const zeile = page.getByRole('table').getByRole('row').filter({ hasText: 'scan-stimmig.pdf' }).filter({ hasText: 'Dublette' })
  await expect(zeile.first()).toBeVisible()
})

test('vierseitigen Scan in 1–2 und 3–4 aufteilen → zwei neue Belege, Original unverändert', async ({ page }) => {
  const inhalt = testdatei('scan-vier-seiten.pdf')
  const vorgang = await hochladenUndWarten(page.request, 'scan-vier-seiten.pdf', inhalt)
  expect(vorgang.status).toBe('Fertig')
  const beleg = await belegLesen(page.request, vorgang.belegId!)
  expect(beleg.seitenanzahl).toBe(4)
  const originalPfad = ablageDateien().find((p) => p.endsWith('_DW-4711.pdf'))
  expect(originalPfad).toBeTruthy()

  const bekannte = new Set((await vorgaenge(page.request)).map((v) => v.id))
  await page.goto(`/belege/${beleg.id}`)
  await page.getByRole('button', { name: 'Weitere Aktionen' }).click()
  await page.getByRole('menuitem', { name: 'Aufteilen …' }).click()
  const dialog = page.getByRole('dialog', { name: 'Beleg aufteilen' })
  await expect(dialog.getByRole('img', { name: /^Seite \d$/ })).toHaveCount(4)
  await dialog.getByRole('button', { name: 'Schnitt nach Seite 2' }).click()
  await expect(dialog.getByRole('list', { name: 'Teile' })).toContainText('A Seiten 1–2 → neuer Beleg')
  await expect(dialog.getByRole('list', { name: 'Teile' })).toContainText('B Seiten 3–4 → neuer Beleg')
  await dialog.getByRole('button', { name: 'In 2 Belege aufteilen' }).click()
  await expect(page.getByText('Beleg wird in 2 Belege aufgeteilt')).toBeVisible()

  const neue = await aufVorgaengeWarten(page.request, (v) => !bekannte.has(v.id), 2)
  expect(neue.map((v) => v.status)).toEqual(['Fertig', 'Fertig'])
  const belege = await Promise.all(neue.map((v) => belegLesen(page.request, v.belegId!)))
  expect(new Set(belege.map((b) => b.id)).size).toBe(2)
  // Jeder Teil liegt mit genau seinen zwei Seiten in der Ablage (die Seitenzahl am Beleg stammt aus dem DI-Ergebnis).
  for (const teil of belege) {
    const datei = (teil.dateien as { rolle: string; pfad: string }[]).find((d) => d.rolle === 'Original')!
    const pdf = await PDFDocument.load(ablageLesen(datei.pfad.replace(/^ablage\//, '')))
    expect(pdf.getPageCount()).toBe(2)
  }
  expect(belege.map((b) => b.id)).not.toContain(beleg.id)

  // Das Original bleibt byte-gleich in der Ablage.
  expect(sha256(ablageLesen(originalPfad!))).toBe(sha256(inhalt))
})
