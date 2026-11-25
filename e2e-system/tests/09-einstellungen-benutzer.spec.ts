import { expect, test } from '@playwright/test'
import { ADMIN_SITZUNG, aufVorgangWarten, belegLesen, einfachesPdf, einstellungenSetzen, hochladen } from '../hilfen/system'

test.use({ storageState: ADMIN_SITZUNG })

test('USt-Status Kleinunternehmer → Vorsteuer-Kachel und Export-Option verschwinden', async ({ page }) => {
  const jahr = new Date().getFullYear()
  await page.goto('/')
  await expect(page.getByRole('heading', { name: `Vorsteuer ${jahr}` })).toBeVisible()

  await page.goto('/einstellungen/umsatzsteuer')
  await page.getByRole('radio', { name: /Kleinunternehmer/ }).check()
  await page.getByRole('button', { name: 'Speichern' }).click()
  await expect(page.getByText('Gespeichert')).toBeVisible()

  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1, name: 'Dashboard', exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: `Vorsteuer ${jahr}` })).toHaveCount(0)
  await page.goto('/export')
  await expect(page.getByRole('checkbox', { name: 'Belegliste' })).toBeVisible()
  await expect(page.getByRole('checkbox', { name: 'Vorsteuer je UStVA-Zeitraum' })).toHaveCount(0)

  await einstellungenSetzen(page.request, 'ust', { status: 'Unbekannt' })
})

test('offene Registrierung → zweites Konto wartet auf Freigabe → Admin gibt als Nur-Lesen frei → keine Bearbeiten-Aktionen', async ({
  page,
  browser,
  baseURL,
}) => {
  const gast = { name: 'Jonas Wagner', email: 'j.wagner@e2e.local', passwort: 'wagner-e2e-passwort' }

  await page.goto('/einstellungen/benutzer')
  const schalter = page.getByRole('switch', { name: 'Offene Registrierung' })
  await expect(schalter).toHaveAttribute('aria-checked', 'false')
  await schalter.click()
  await expect(schalter).toHaveAttribute('aria-checked', 'true')

  // Eigener Kontext ohne die Admin-Sitzung (test.use gilt sonst auch für browser.newContext).
  const kontext = await browser.newContext({
    baseURL,
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    storageState: { cookies: [], origins: [] },
  })
  const zweiter = await kontext.newPage()
  try {
    await zweiter.goto('/anmelden')
    await zweiter.getByRole('tab', { name: 'Konto erstellen' }).click()
    await zweiter.getByLabel('Name').fill(gast.name)
    await zweiter.getByLabel('E-Mail-Adresse').fill(gast.email)
    await zweiter.getByLabel('Passwort', { exact: true }).fill(gast.passwort)
    await zweiter.getByRole('button', { name: 'Konto erstellen' }).click()
    await expect(zweiter.getByRole('heading', { name: 'Konto wartet auf Freigabe' })).toBeVisible()

    // Anmelden vor der Freigabe → wieder der Hinweis.
    await zweiter.getByRole('button', { name: 'Zur Anmeldung' }).click()
    await zweiter.getByLabel('E-Mail-Adresse').fill(gast.email)
    await zweiter.getByLabel('Passwort', { exact: true }).fill(gast.passwort)
    await zweiter.getByRole('button', { name: 'Anmelden' }).click()
    await expect(zweiter.getByRole('heading', { name: 'Konto wartet auf Freigabe' })).toBeVisible()

    // Admin gibt frei.
    await page.reload()
    const balken = page.getByRole('alert').filter({ hasText: `${gast.name} hat sich registriert und wartet auf Freigabe.` })
    await expect(balken).toBeVisible()
    await balken.getByLabel(`Rolle für ${gast.name}`).selectOption('NurLesen')
    await balken.getByRole('button', { name: 'Freigeben' }).click()
    await expect(page.getByText(`${gast.name} freigegeben`)).toBeVisible()
    await expect(page.getByLabel(`Rolle von ${gast.name}`)).toHaveValue('NurLesen')

    // Nur-Lesen meldet sich an und sieht keine Bearbeiten-Aktionen.
    await zweiter.getByRole('button', { name: 'Zur Anmeldung' }).click()
    await zweiter.getByLabel('E-Mail-Adresse').fill(gast.email)
    await zweiter.getByLabel('Passwort', { exact: true }).fill(gast.passwort)
    await zweiter.getByRole('button', { name: 'Anmelden' }).click()
    await expect(zweiter.getByRole('heading', { level: 1, name: 'Dashboard', exact: true })).toBeVisible()

    const belege = (await (await zweiter.request.get('/api/belege', { params: { seitengroesse: 50 } })).json()) as {
      eintraege: { id: string; rechnungsnummer: string }[]
    }
    const beleg = belege.eintraege.find((b) => b.rechnungsnummer === 'DW-4711') ?? belege.eintraege[0]
    await zweiter.goto(`/belege/${beleg.id}`)
    await expect(zweiter.getByRole('heading', { level: 1 })).toBeVisible()
    await expect(zweiter.getByRole('button', { name: 'Erneut auslesen' })).toHaveCount(0)
    await expect(zweiter.getByRole('link', { name: 'Aufteilen' })).toHaveCount(0)
    await expect(zweiter.getByRole('button', { name: 'Bestätigen' })).toHaveCount(0)
    await zweiter.goto('/tags')
    await expect(zweiter.getByRole('table').first()).toBeVisible()
    await expect(zweiter.getByRole('button', { name: 'Neuer Tag' })).toHaveCount(0)
    const schreiben = await zweiter.request.post('/api/tags', {
      data: { name: 'verboten', farbe: '#16A34A', istInbox: false, istZahlungsTag: false, matching: { algorithmus: 'Keiner', muster: null, grossKlein: false } },
    })
    expect(schreiben.status()).toBe(403)
  } finally {
    await kontext.close()
  }
})

test('DI-Limit = Verbrauch → nächster Scan pausiert mit Benachrichtigung → nach Limit-Erhöhung fortsetzen', async ({ page }) => {
  const verbrauch = (await (await page.request.get('/api/einstellungen/di/verbrauch')).json()) as { seiten: number }
  expect(verbrauch.seiten).toBeGreaterThan(0)

  await page.goto('/einstellungen/document-intelligence')
  const limit = page.getByLabel('Monatslimit (Seiten)')
  await limit.fill(String(verbrauch.seiten))
  await page.getByRole('button', { name: 'Speichern' }).click()
  await expect(page.getByText('Gespeichert')).toBeVisible()

  const [angenommen] = await hochladen(page.request, { name: 'limit-scan.pdf', inhalt: await einfachesPdf('DI-Limit E2E') })
  const pausiert = await aufVorgangWarten(page.request, angenommen.id)
  expect(pausiert.status).toBe('Pausiert')

  await page.reload()
  await expect(page.getByText(/Warteschlange: pausiert · 1 wartend/)).toBeVisible()
  await page.getByRole('button', { name: /^Benachrichtigungen, \d+ ungelesen$/ }).click()
  const glocke = page.getByRole('dialog', { name: 'Benachrichtigungen' })
  await expect(glocke.getByText('DI-Limit erreicht – Warteschlange pausiert').first()).toBeVisible()
  await page.keyboard.press('Escape')

  await limit.fill(String(verbrauch.seiten + 100))
  await page.getByRole('button', { name: 'Speichern' }).click()
  await expect(page.getByText('Gespeichert').first()).toBeVisible()
  await page.getByRole('button', { name: 'Warteschlange jetzt fortsetzen' }).click()

  await expect
    .poll(async () => ((await (await page.request.get(`/api/import/vorgaenge/${angenommen.id}`)).json()) as { status: string }).status, {
      timeout: 90_000,
      intervals: [1000],
    })
    .toBe('Fertig')
  const fertig = await aufVorgangWarten(page.request, angenommen.id)
  expect((await belegLesen(page.request, fertig.belegId!)).id).toBe(fertig.belegId)
})
