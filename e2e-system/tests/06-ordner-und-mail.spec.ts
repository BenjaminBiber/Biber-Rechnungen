import { expect, test } from '@playwright/test'
import { mailSenden, mailsImOrdner } from '../hilfen/mail'
import {
  ADMIN_SITZUNG,
  aufVorgaengeWarten,
  belegLesen,
  einfachesPdf,
  einstellungenSetzen,
  hochladen,
  testdatei,
  vorgaenge,
} from '../hilfen/system'
import { eingangAblegen, eingangDateien } from '../hilfen/werkzeug'

test.use({ storageState: ADMIN_SITZUNG })

test('Datei in den Eingangsordner → Beleg entsteht, Eingang ist danach leer', async ({ page }) => {
  await einstellungenSetzen(page.request, 'ordner', { pollingSekunden: 5, stabilSekunden: 1 })
  const bekannte = new Set((await vorgaenge(page.request)).map((v) => v.id))

  eingangAblegen('scanner-ordner.pdf', await einfachesPdf('Ordner-Import E2E'))

  const [vorgang] = await aufVorgaengeWarten(page.request, (v) => !bekannte.has(v.id) && v.quelle === 'Ordner', 1)
  expect(vorgang.status).toBe('Fertig')
  expect(vorgang.dateiname).toBe('scanner-ordner.pdf')
  expect((await belegLesen(page.request, vorgang.belegId!)).id).toBe(vorgang.belegId)
  expect(eingangDateien()).toEqual([])
})

test('Mail mit PDF-Anhang → Beleg mit Quelle E-Mail, Mail liegt in „Verarbeitet“; ohne Anhang → „Ohne Beleg“', async ({ page }) => {
  await einstellungenSetzen(page.request, 'mail', { aktiv: true, abrufintervallMinuten: 1 })
  const bekannte = new Set((await vorgaenge(page.request)).map((v) => v.id))

  await mailSenden('Rechnung per Mail E2E', [
    { dateiname: 'rechnung-mail.pdf', inhalt: await einfachesPdf('Mail-Import E2E'), typ: 'application/pdf' },
  ])
  await mailSenden('Nur eine Frage, kein Beleg')

  const abruf = await page.request.post('/api/einstellungen/mail/jetzt-abrufen')
  expect(abruf.ok(), await abruf.text()).toBeTruthy()

  const neue = await aufVorgaengeWarten(page.request, (v) => !bekannte.has(v.id) && v.quelle === 'Mail', 2)
  const mitAnhang = neue.find((v) => v.dateiname === 'rechnung-mail.pdf')
  expect(mitAnhang?.status).toBe('Fertig')
  const ohneAnhang = neue.find((v) => v.status === 'OhneBeleg')
  expect(ohneAnhang).toBeTruthy()

  // Quelle E-Mail steht am Beleg und in seiner Historie.
  expect((await belegLesen(page.request, mitAnhang!.belegId!)).quelle).toBe('Mail')
  await page.goto(`/belege/${mitAnhang!.belegId}/historie`)
  await expect(page.getByRole('listitem').filter({ hasText: 'Importiert aus E-Mail: rechnung-mail.pdf' })).toBeVisible()

  await expect.poll(() => mailsImOrdner('Verarbeitet')).toContain('Rechnung per Mail E2E')
  await expect.poll(() => mailsImOrdner('Ohne Beleg')).toContain('Nur eine Frage, kein Beleg')
  expect(await mailsImOrdner('INBOX')).toEqual([])
})

test('foto.heic hochladen → abgelehnt mit Meldung und Benachrichtigung in der Glocke', async ({ page }) => {
  // Im Dialog „Hochladen“ wird das Format schon im Browser abgelehnt …
  await page.goto('/hochladen')
  await page.getByLabel('Dateien auswählen').setInputFiles({ name: 'foto.heic', mimeType: 'image/heic', buffer: testdatei('foto.heic') })
  await expect(page.getByText('Format nicht unterstützt. Erlaubt sind PDF, XML, PNG, JPG und TIFF.')).toBeVisible()

  // … am Upload-Endpunkt (wie bei Clients ohne diese Prüfung) entsteht ein abgelehnter Vorgang mit Benachrichtigung.
  const [vorgang] = await hochladen(page.request, { name: 'foto.heic', inhalt: testdatei('foto.heic') })
  expect(vorgang.status).toBe('Abgelehnt')

  await page.goto('/')
  await page.getByRole('button', { name: /^Benachrichtigungen, \d+ ungelesen$/ }).click()
  const glocke = page.getByRole('dialog', { name: 'Benachrichtigungen' })
  await expect(glocke.getByText('Import abgelehnt').first()).toBeVisible()
  await expect(glocke.getByText(/foto\.heic/).first()).toBeVisible()
})
