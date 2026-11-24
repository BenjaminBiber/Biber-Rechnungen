import { expect, test, type Page } from '@playwright/test'
import { ADMIN_SITZUNG } from '../hilfen/system'

test.use({ storageState: ADMIN_SITZUNG, viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })

async function scrolltNichtHorizontal(page: Page) {
  await page.evaluate(() => document.fonts.ready)
  await expect
    .poll(() => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth))
    .toBeLessThanOrEqual(0)
}

async function zaehler(page: Page) {
  return (await (await page.request.get('/api/belege/zaehler')).json()) as { alle: number; offen: number; zuPruefen: number }
}

test('Übersicht, Belegliste mit Filter-Sheet und Beleg bestätigen auf 390 × 844', async ({ page }) => {
  const vorher = await zaehler(page)
  expect(vorher.zuPruefen).toBeGreaterThan(0)

  await page.goto('/')
  await expect(page.getByText('Gesamtkosten pro Monat')).toBeVisible()
  await scrolltNichtHorizontal(page)

  // Filter-Sheet: „Offen“ wählen.
  await page.goto('/belege')
  const liste = page.getByRole('list', { name: 'Belege' })
  await expect(liste.getByRole('listitem').first()).toBeVisible()
  await page.getByRole('button', { name: 'Filter' }).click()
  const sheet = page.getByRole('dialog', { name: 'Filtern' })
  const offen = sheet.getByRole('radio', { name: /^Offen \d+$/ })
  await offen.click()
  const anzahlOffen = Number((await offen.textContent())?.match(/\d+/)?.[0])
  expect(anzahlOffen).toBe(vorher.offen)
  await sheet.getByRole('button', { name: `${anzahlOffen} Belege anzeigen` }).click()
  await expect(sheet).toBeHidden()
  await expect(page).toHaveURL(/ansicht=Offen/)
  await expect(liste.getByRole('listitem')).toHaveCount(Math.min(anzahlOffen, 50))
  await expect(page.getByRole('button', { name: 'Filter, 1 aktiv' })).toBeVisible()
  await scrolltNichtHorizontal(page)

  // Über die Kachel „Zu prüfen“ zur Liste und einen Beleg bestätigen.
  await page.goto('/')
  await page.getByRole('link', { name: new RegExp(`^Zu prüfen\\s*${vorher.zuPruefen}$`) }).click()
  await expect(page).toHaveURL(/ansicht=ZuPruefen/)
  // Die Zähler lassen Dubletten weg, die Liste zeigt sie (Dublette aus Test 05).
  const zuPruefenListe = (await (
    await page.request.get('/api/belege', { params: { ansicht: 'ZuPruefen', seitengroesse: 200 } })
  ).json()) as { gesamt: number; eintraege: { id: string; istDublette: boolean }[] }
  expect(zuPruefenListe.eintraege.filter((b) => !b.istDublette)).toHaveLength(vorher.zuPruefen)
  await expect(liste.getByRole('listitem')).toHaveCount(zuPruefenListe.gesamt)
  const ersterEchter = zuPruefenListe.eintraege.find((b) => !b.istDublette)!
  await liste.locator(`a[href="/belege/${ersterEchter.id}"]`).click()
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  await scrolltNichtHorizontal(page)

  await page.getByRole('button', { name: 'Bestätigen' }).click()
  const rueckfrage = page.getByRole('alertdialog')
  await expect(rueckfrage).toBeVisible()
  await rueckfrage.getByRole('button', { name: 'Trotzdem bestätigen' }).click()
  await expect(page.getByText('Beleg bestätigt')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Bestätigen' })).toHaveCount(0)

  expect((await zaehler(page)).zuPruefen).toBe(vorher.zuPruefen - 1)
})
