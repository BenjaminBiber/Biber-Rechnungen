import { screen, waitFor, within } from '@testing-library/react'
import { http } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { belegIds, kategorieIds, korrespondentIds, tagIds } from '@/mocks/fixtures/ids'
import { problem } from '@/mocks/handlers/gemeinsam'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { FEHLER_BETRAG, FEHLER_DATUM } from './formular'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

const formular = () => screen.getByRole('form', { name: 'Daten' })
const feld = (label: string) => within(formular()).getByLabelText(label)
const speichern = () => screen.getByRole('button', { name: 'Änderungen speichern' })

async function oeffneDaten(pfad?: string) {
  const ergebnis = renderBelegDetail(pfad)
  await kopfGeladen()
  await screen.findByRole('form', { name: 'Daten' })
  return ergebnis
}

describe('Tab „Daten“', () => {
  it('füllt alle Felder aus dem Beleg', async () => {
    await oeffneDaten()
    expect(feld('Korrespondent')).toHaveTextContent('Bürowelt Krämer KG')
    expect(within(formular()).getByText('erkannt über USt-IdNr.')).toBeInTheDocument()
    expect(feld('Belegart')).toHaveValue('Rechnung')
    expect(feld('Rechnungsnummer')).toHaveValue('2026-48817')
    expect(feld('Rechnungsdatum')).toHaveValue('19.11.2026')
    expect(feld('Leistungsdatum / -zeitraum')).toHaveValue('19.11.2026')
    expect(feld('Fällig am')).toHaveValue('03.12.2026')
    expect(feld('Kategorie')).toHaveTextContent('Bürobedarf')
    expect(within(formular()).getByText('manuell geändert · Default: Bürobedarf')).toBeInTheDocument()
    expect(feld('Währung')).toHaveValue('EUR')
    expect(feld('Netto')).toHaveValue('221,85')
    expect(feld('USt')).toHaveValue('42,15')
    expect(feld('Brutto')).toHaveValue('264,00')
    const zeilen = within(screen.getByRole('table', { name: 'Steuerzeilen' })).getAllByRole('row')
    expect(zeilen).toHaveLength(2)
    expect(within(zeilen[1]).getByLabelText('Steuersatz Steuerzeile 1')).toHaveValue('19')
    expect(within(zeilen[1]).getByText('stimmt')).toBeInTheDocument()
    expect(formular()).toHaveTextContent(
      'Ausgelesen mit Document Intelligence (prebuilt-invoice) · 19.11.2026 08:14 · 1 Seite',
    )
    expect(within(formular()).getByRole('meter', { name: 'Konfidenz' })).toHaveAttribute('aria-valuenow', '0.74')
    expect(formular()).toHaveTextContent('0,74')
    expect(speichern()).toBeDisabled()
  })

  it('zeigt für einen per Auto-Matching zugeordneten Korrespondenten „automatisch zugeordnet“', async () => {
    await oeffneDaten(`/belege/${belegIds.b12}`)
    expect(within(formular()).getByText('automatisch zugeordnet')).toBeInTheDocument()
    expect(within(formular()).queryByText(/erkannt über/)).not.toBeInTheDocument()
  })

  it('sendet beim Speichern das erwartete BelegAendernDto und meldet „Gespeichert“', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Rechnungsnummer'))
    await nutzer.type(feld('Rechnungsnummer'), '2026-48818')
    await nutzer.clear(feld('Leistungsdatum / -zeitraum'))
    await nutzer.type(feld('Leistungsdatum / -zeitraum'), '01.11.2026 – 15.11.2026')
    await nutzer.selectOptions(feld('Belegart'), 'Gutschrift')
    await nutzer.click(feld('Kategorie'))
    await nutzer.click(await screen.findByRole('option', { name: /Hardware/ }))
    expect(speichern()).toBeEnabled()
    await nutzer.click(speichern())

    expect(await screen.findByText('Gespeichert')).toBeInTheDocument()
    const puts = (await anfragen()).filter((a) => a.methode === 'PUT')
    expect(puts).toEqual([
      {
        methode: 'PUT',
        pfad: `/api/belege/${b2}`,
        body: {
          korrespondentId: korrespondentIds.bueroweltKraemer,
          belegart: 'Gutschrift',
          rechnungsnummer: '2026-48818',
          titel: 'Büromaterial Bestellung',
          rechnungsdatum: '2026-11-19',
          leistungsdatum: null,
          leistungVon: '2026-11-01',
          leistungBis: '2026-11-15',
          faelligkeitsdatum: '2026-12-03',
          kategorieId: kategorieIds.hardware,
          waehrung: 'EUR',
          netto: 221.85,
          steuer: 42.15,
          brutto: 264,
          steuerzeilen: [{ satz: 19, netto: 221.85, steuer: 42.15 }],
        },
      },
    ])
    await waitFor(() => expect(speichern()).toBeDisabled())
    expect(feld('Kategorie')).toHaveTextContent('Hardware')
    expect(feld('Leistungsdatum / -zeitraum')).toHaveValue('01.11.2026 – 15.11.2026')
  })

  it('lehnt ungültiges Datum und ungültigen Betrag ab', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Rechnungsdatum'))
    await nutzer.type(feld('Rechnungsdatum'), '31.02.2026')
    await nutzer.clear(feld('Netto'))
    await nutzer.type(feld('Netto'), '221.8x')
    await nutzer.click(speichern())

    expect(feld('Rechnungsdatum')).toHaveAttribute('aria-invalid', 'true')
    expect(feld('Rechnungsdatum')).toHaveAccessibleDescription(FEHLER_DATUM)
    expect(feld('Netto')).toHaveAccessibleDescription(FEHLER_BETRAG)
    expect((await anfragen()).filter((a) => a.methode === 'PUT')).toHaveLength(0)

    await nutzer.clear(feld('Rechnungsdatum'))
    await nutzer.type(feld('Rechnungsdatum'), '20.11.2026')
    expect(feld('Rechnungsdatum')).not.toHaveAttribute('aria-invalid')
  })

  it('zeigt Serverfehler am Feld', async () => {
    server.use(
      http.put('*/api/belege/:id', () =>
        problem(400, 'Die Eingabe ist ungültig.', { Rechnungsnummer: ['Rechnungsnummer ist bereits vergeben.'] }),
      ),
    )
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), '-1')
    await nutzer.click(speichern())
    expect(await screen.findByText('Rechnungsnummer ist bereits vergeben.')).toBeInTheDocument()
    expect(feld('Rechnungsnummer')).toHaveAccessibleDescription('Rechnungsnummer ist bereits vergeben.')
  })

  it('zeigt beim Leeren der Kategorie den Default-Hinweis', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.click(feld('Kategorie'))
    // Erneutes Wählen der aktuellen Kategorie leert das Feld.
    await nutzer.click(await screen.findByRole('option', { name: /Bürobedarf/ }))
    expect(feld('Kategorie')).toHaveTextContent('Keine Kategorie')
    expect(within(formular()).getByText('leer · Default: Bürobedarf')).toBeInTheDocument()
    expect(speichern()).toBeEnabled()
  })

  it('fügt eine Steuerzeile hinzu und zeigt „weicht ab“', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.click(screen.getByRole('button', { name: '+ Steuerzeile' }))
    await nutzer.clear(screen.getByLabelText('Steuersatz Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuersatz Steuerzeile 2'), '7')
    await nutzer.type(screen.getByLabelText('Netto Steuerzeile 2'), '100,00')
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '7,50')
    const zeile = screen.getByLabelText('Netto Steuerzeile 2').closest('tr')!
    expect(within(zeile).getByText('weicht ab')).toBeInTheDocument()
    await nutzer.clear(screen.getByLabelText('Steuer Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '7,01')
    expect(within(zeile).getByText('stimmt')).toBeInTheDocument()

    await nutzer.click(screen.getByRole('button', { name: 'Steuerzeile 2 entfernen' }))
    expect(screen.queryByLabelText('Netto Steuerzeile 2')).not.toBeInTheDocument()
  })

  it('„Verwerfen“ setzt das Formular zurück', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'X')
    expect(speichern()).toBeEnabled()
    await nutzer.click(screen.getByRole('button', { name: 'Verwerfen' }))
    expect(feld('Rechnungsnummer')).toHaveValue('2026-48817')
    expect(speichern()).toBeDisabled()
  })

  it('ändert Tags sofort; „zu prüfen“ ist nicht entfernbar', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    const tags = within(formular()).getByText('Tags').nextElementSibling as HTMLElement
    const zuPruefen = within(tags).getByText('zu prüfen')
    expect(zuPruefen.closest('[title]')).toHaveAttribute('title', 'System-Tag, wird nur durch Bestätigen entfernt')
    expect(within(tags).queryByRole('button', { name: 'zu prüfen entfernen' })).not.toBeInTheDocument()

    await nutzer.click(within(tags).getByRole('button', { name: 'Inbox entfernen' }))
    await waitFor(() => expect(within(tags).queryByText('Inbox')).not.toBeInTheDocument())

    await nutzer.click(within(tags).getByRole('button', { name: '+ Tag hinzufügen' }))
    await nutzer.click(await screen.findByRole('option', { name: /Reisekosten/ }))
    await waitFor(() => expect(within(tags).getByText('Reisekosten')).toBeInTheDocument())

    expect((await anfragen()).map((a) => [a.methode, a.pfad, a.body])).toEqual([
      ['DELETE', `/api/belege/${b2}/tags/${tagIds.inbox}`, undefined],
      ['POST', `/api/belege/${b2}/tags`, { tagId: tagIds.reise }],
    ])
    expect(speichern()).toBeDisabled()
  })

  it('fragt beim Verlassen mit ungespeicherten Änderungen nach', async () => {
    const { nutzer, router } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'X')
    const tabs = screen.getByRole('navigation', { name: 'Belegdaten' })
    await nutzer.click(within(tabs).getByRole('link', { name: /^Positionen/ }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Ungespeicherte Änderungen verwerfen?' })
    await nutzer.click(within(dialog).getByRole('button', { name: 'Weiter bearbeiten' }))
    expect(router.state.location.pathname).toBe(`/belege/${b2}`)
    expect(feld('Rechnungsnummer')).toHaveValue('2026-48817X')

    await nutzer.click(within(tabs).getByRole('link', { name: /^Positionen/ }))
    const erneut = await screen.findByRole('alertdialog', { name: 'Ungespeicherte Änderungen verwerfen?' })
    await nutzer.click(within(erneut).getByRole('button', { name: 'Verwerfen und verlassen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}/positionen`))
  })

  it('ist für NurLesen schreibgeschützt und ohne Speichern-Leiste', async () => {
    szenario.rolle('NurLesen')
    await oeffneDaten()
    for (const label of ['Rechnungsnummer', 'Rechnungsdatum', 'Leistungsdatum / -zeitraum', 'Fällig am', 'Netto'])
      expect(feld(label)).toHaveAttribute('readonly')
    expect(feld('Belegart')).toBeDisabled()
    expect(feld('Korrespondent')).toBeDisabled()
    expect(feld('Kategorie')).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Änderungen speichern' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Tag hinzufügen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Inbox entfernen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Steuerzeile' })).not.toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Steuerzeilen' })).toHaveTextContent('19 %')
  })

  it('zeigt bei E-Rechnungen die Herkunft statt der Konfidenz', async () => {
    renderBelegDetail(`/belege/${belegIds.b1}`)
    await kopfGeladen('Hostwerk Süd GmbH')
    const daten = await screen.findByRole('form', { name: 'Daten' })
    expect(daten).toHaveTextContent('Aus E-Rechnung (XRechnung) übernommen')
    expect(within(daten).queryByRole('meter')).not.toBeInTheDocument()
  })
})
