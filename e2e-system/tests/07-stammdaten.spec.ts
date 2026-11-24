import { expect, test, type APIRequestContext } from '@playwright/test'
import { ADMIN_SITZUNG } from '../hilfen/system'

test.use({ storageState: ADMIN_SITZUNG })

const keinMatching = { algorithmus: 'Keiner', muster: null, grossKlein: false }

async function liste<T>(request: APIRequestContext, pfad: string): Promise<T[]> {
  const antwort = await request.get(pfad, { params: { seitengroesse: 200 } })
  expect(antwort.status(), await antwort.text()).toBe(200)
  const daten = await antwort.json()
  return Array.isArray(daten) ? daten : daten.eintraege
}

async function zaehler(request: APIRequestContext) {
  return (await (await request.get('/api/belege/zaehler')).json()) as { offen: number; alle: number }
}

test('Tag „bezahlt“ als Zahlungs-Tag → Beleg bezahlt markieren → verschwindet aus „Offen“', async ({ page }) => {
  await page.goto('/tags')
  await page.getByRole('button', { name: 'Neuer Tag' }).click()
  const dialog = page.getByRole('dialog', { name: 'Neuer Tag' })
  await dialog.getByLabel('Name').fill('bezahlt')
  await dialog.getByRole('radio', { name: 'Grün' }).click()
  await dialog.getByRole('radio', { name: /Zahlungs-Tag/ }).check()
  await dialog.getByRole('button', { name: 'Speichern' }).click()
  await expect(dialog).toBeHidden()
  await expect(page.locator('table tbody tr').filter({ hasText: 'bezahlt' })).toContainText('Zahlungs-Tag')

  const offenVorher = (await zaehler(page.request)).offen
  expect(offenVorher).toBeGreaterThan(0)

  // In der Ansicht „Offen“ über das Zeilenmenü als bezahlt markieren.
  await page.goto('/belege?ansicht=Offen')
  const zeile = page.getByRole('row').filter({ hasText: 'SCH-2026-0815' })
  await expect(zeile).toBeVisible()
  await zeile.getByRole('button', { name: /^Aktionen für/ }).click()
  await page.getByRole('menuitem', { name: /Als bezahlt markieren/ }).focus()
  await page.keyboard.press('ArrowRight')
  await expect(page.getByRole('menuitem', { name: 'bezahlt', exact: true })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page.getByText('Als „bezahlt“ markiert')).toBeVisible()

  expect((await zaehler(page.request)).offen).toBe(offenVorher - 1)
  await page.goto('/belege?ansicht=Offen')
  await expect(page.getByRole('row').filter({ hasText: 'SCH-2026-0815' })).toHaveCount(0)
  await page.goto('/')
  const offenKarte = page.getByRole('link').filter({ hasText: 'ohne Zahlungs-Tag' })
  await expect(offenKarte.getByText(String(offenVorher - 1), { exact: true })).toBeVisible()
})

test('Kategorie mit EÜR-Zeile, Matching-Regel testen und anwenden', async ({ page }) => {
  await page.goto('/tags')
  await page.getByRole('tab', { name: /Kategorien/ }).click()
  await page.getByRole('button', { name: 'Neue Kategorie' }).click()
  const dialog = page.getByRole('dialog', { name: 'Neue Kategorie' })
  await dialog.getByLabel('Name').fill('Werkzeug')
  await dialog.getByLabel('EÜR-Zeile (optional)').fill('Sonstige Betriebsausgaben')
  await dialog.getByRole('button', { name: 'Speichern' }).click()
  await expect(dialog).toBeHidden()
  await expect(page.locator('table tbody tr').filter({ hasText: 'Werkzeug' })).toContainText('Sonstige Betriebsausgaben')

  const kategorie = (await liste<{ id: string; name: string }>(page.request, '/api/kategorien')).find((k) => k.name === 'Werkzeug')!
  const matching = { algorithmus: 'EinWort', muster: 'Schraubendreher', grossKlein: false }
  const test = await page.request.post('/api/matching/testen', { data: { ziel: 'Kategorie', zielId: kategorie.id, matching } })
  expect(test.status(), await test.text()).toBe(200)
  const ergebnis = (await test.json()) as { treffer: number }
  expect(ergebnis.treffer).toBeGreaterThan(0)

  const speichern = await page.request.put(`/api/kategorien/${kategorie.id}`, {
    data: { name: 'Werkzeug', farbe: '#16A34A', euerZeile: 'Sonstige Betriebsausgaben', matching },
  })
  expect(speichern.ok(), await speichern.text()).toBeTruthy()
  const anwenden = await page.request.post('/api/matching/anwenden', { data: { ziel: 'Kategorie', zielId: kategorie.id } })
  expect(anwenden.status(), await anwenden.text()).toBe(200)
  expect(((await anwenden.json()) as { geaendert: number }).geaendert).toBe(ergebnis.treffer)

  await page.goto('/tags')
  await page.getByRole('tab', { name: /Kategorien/ }).click()
  await expect(page.locator('table tbody tr').filter({ hasText: 'Werkzeug' })).toContainText(String(ergebnis.treffer))
})

test('zwei Korrespondenten zusammenführen', async ({ page }) => {
  const anlegen = await page.request.post('/api/korrespondenten', {
    data: {
      name: 'Krause Werkzeuge (alt)',
      aliase: [],
      anschrift: null,
      ustIdNr: null,
      steuernummer: null,
      ibans: [],
      website: null,
      email: null,
      istLieferant: true,
      istKunde: false,
      defaultKategorieId: null,
      matching: keinMatching,
    },
  })
  expect(anlegen.status(), await anlegen.text()).toBe(201)
  const alt = (await anlegen.json()) as { id: string }
  const korrespondenten = await liste<{ id: string; name: string }>(page.request, '/api/korrespondenten')
  const krause = korrespondenten.find((k) => k.name === 'Werkzeughandel Krause GmbH')!

  await page.goto('/korrespondenten')
  await expect(page.getByText('Krause Werkzeuge (alt)')).toBeVisible()
  const zusammen = await page.request.post('/api/korrespondenten/zusammenfuehren', { data: { behaltenId: krause.id, quellIds: [alt.id] } })
  expect(zusammen.ok(), await zusammen.text()).toBeTruthy()

  const danach = await liste<{ id: string; name: string }>(page.request, '/api/korrespondenten')
  expect(danach.map((k) => k.id)).not.toContain(alt.id)
  const krauseDetail = await (await page.request.get(`/api/korrespondenten/${krause.id}`)).json()
  expect(krauseDetail.aliase).toContain('Krause Werkzeuge (alt)')
})

test('Stammartikel aus zwei Artikeln → Preisverlauf mit zwei Serien', async ({ page }) => {
  const artikel = await liste<{ id: string; name: string; korrespondent: { name: string } }>(page.request, '/api/artikel')
  const papier = artikel.find((a) => a.name === 'Druckerpapier A4')
  const bohrer = artikel.find((a) => a.name === 'Bohrer-Set')
  expect(papier, artikel.map((a) => a.name).join(', ')).toBeTruthy()
  expect(bohrer).toBeTruthy()

  const anlegen = await page.request.post('/api/stammartikel', {
    data: {
      name: 'Bürobedarf allgemein',
      basiseinheit: 'Stück',
      grundpreisMenge: 1,
      artikel: [{ artikelId: papier!.id, umrechnungFaktor: null }, { artikelId: bohrer!.id, umrechnungFaktor: null }],
    },
  })
  expect(anlegen.status(), await anlegen.text()).toBe(201)
  const stamm = (await anlegen.json()) as { id: string }

  const detail = await (await page.request.get(`/api/stammartikel/${stamm.id}`)).json()
  expect(detail.serien).toHaveLength(2)

  await page.goto(`/artikel/stammartikel/${stamm.id}`)
  await expect(page.getByRole('heading', { level: 1, name: 'Bürobedarf allgemein' })).toBeVisible()
  await expect(page.getByText('Bürobedarf Schulz GmbH').first()).toBeVisible()
  await expect(page.getByText('Werkzeughandel Krause GmbH').first()).toBeVisible()
})
