import { screen, waitFor, within } from '@testing-library/react'
import { http } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { belegIds, kategorieIds, korrespondentIds, tagIds } from '@/mocks/fixtures/ids'
import { problem } from '@/mocks/handlers/gemeinsam'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { szenario } from '@/mocks/szenarien'
import { FEHLER_BETRAG, FEHLER_DATUM, FEHLER_NEGATIV, fehlerSatzDoppelt } from './formular'
import { anfragenMitschneiden, b2, kopfGeladen, renderBelegDetail } from './testHilfe'

vi.mock('react-pdf', () => import('./testHilfe').then((m) => m.reactPdfMock))

const formular = () => screen.getByRole('form', { name: 'Daten' })
const feld = (label: string) => within(formular()).getByLabelText(label)
const status = () => within(formular()).getByRole('status')
const puts = async (anfragen: () => Promise<{ methode: string; body: unknown }[]>) =>
  (await anfragen()).filter((a) => a.methode === 'PUT')

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
    expect(feld('Titel')).toHaveValue('Büromaterial Bestellung')
    expect(feld('Belegart')).toHaveValue('Rechnung')
    expect(feld('Rechnungsnummer')).toHaveValue('2026-48817')
    expect(feld('Rechnungsdatum')).toHaveValue('19.11.2026')
    expect(feld('Leistungsdatum / -zeitraum')).toHaveValue('19.11.2026')
    expect(feld('Fällig am')).toHaveValue('03.12.2026')
    expect(feld('Kategorie')).toHaveTextContent('Bürobedarf')
    expect(within(formular()).getByText('manuell geändert · Default: Bürobedarf')).toBeInTheDocument()
    expect(feld('Währung')).toHaveValue('EUR')
    const zeilen = within(screen.getByRole('table', { name: 'Beträge nach Steuersatz' })).getAllByRole('row')
    // Kopf, eine Steuerzeile, Summe
    expect(zeilen).toHaveLength(3)
    expect(within(zeilen[1]).getByLabelText('Steuersatz Steuerzeile 1')).toHaveValue('19')
    expect(within(zeilen[1]).getByLabelText('Netto Steuerzeile 1')).toHaveValue('221,85')
    expect(within(zeilen[1]).getByLabelText('Steuer Steuerzeile 1')).toHaveValue('42,15')
    expect(within(zeilen[1]).getAllByRole('cell')[3]).toHaveTextContent('264,00')
    expect(
      within(zeilen[1]).getByRole('img', { name: 'Steuer passt: 19 % von 221,85 € = 42,15 €' }),
    ).toBeInTheDocument()
    for (const name of ['Netto', 'USt', 'Brutto'])
      expect(within(zeilen[2]).getByRole('button', { name: new RegExp(`^${name} stimmt`) })).toBeInTheDocument()
    expect(within(formular()).getByText('Alles stimmig')).toBeInTheDocument()
    expect(formular()).toHaveTextContent(
      'Ausgelesen mit Document Intelligence (prebuilt-invoice) · 19.11.2026 08:14 · 1 Seite',
    )
    expect(within(formular()).getByRole('meter', { name: 'Konfidenz' })).toHaveAttribute('aria-valuenow', '0.74')
    expect(formular()).toHaveTextContent('0,74')
    expect(status()).toHaveTextContent('Änderungen werden automatisch gespeichert')
  })

  it('zeigt bei einem Beleg „zu prüfen“ die ausgelesene IBAN mit Bank, BIC und Abgleich', async () => {
    await oeffneDaten()
    expect(feld('IBAN laut Beleg')).toHaveValue('DE12 6805 0101 0012 3456 78')
    expect(feld('IBAN laut Beleg')).toHaveAttribute('readonly')
    expect(feld('IBAN laut Beleg')).toHaveAccessibleDescription(
      'Sparkasse Freiburg-Nördlicher Breisgau · BIC FRSPDE66XXX',
    )
    expect(feld('IBAN laut Beleg 2')).toHaveValue('DE89 3704 0044 0532 0130 00')
    expect(feld('IBAN laut Beleg 2')).toHaveAccessibleDescription('Commerzbank')
    expect(within(formular()).getByText('alle beim Korrespondenten hinterlegt')).toBeInTheDocument()
  })

  it('zeigt für einen per Auto-Matching zugeordneten Korrespondenten „automatisch zugeordnet“', async () => {
    await oeffneDaten(`/belege/${belegIds.b12}`)
    expect(within(formular()).getByText('automatisch zugeordnet')).toBeInTheDocument()
    expect(within(formular()).queryByText(/erkannt über/)).not.toBeInTheDocument()
    expect(within(formular()).queryByLabelText('IBAN laut Beleg')).not.toBeInTheDocument()
  })

  it('speichert Textfelder beim Verlassen, Auswahlfelder sofort, und meldet „Gespeichert“', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Rechnungsnummer'))
    await nutzer.type(feld('Rechnungsnummer'), '2026-48818')
    await nutzer.clear(feld('Leistungsdatum / -zeitraum'))
    await nutzer.type(feld('Leistungsdatum / -zeitraum'), '01.11.2026 – 15.11.2026')
    await nutzer.selectOptions(feld('Belegart'), 'Gutschrift')
    await nutzer.click(feld('Kategorie'))
    await nutzer.click(await screen.findByRole('option', { name: /Hardware/ }))

    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    const gesendet = await puts(anfragen)
    // Rechnungsnummer und Leistung beim Verlassen, Belegart und Kategorie bei der Auswahl.
    expect(gesendet).toHaveLength(4)
    expect(gesendet.at(-1)).toEqual({
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
    })
    expect(feld('Kategorie')).toHaveTextContent('Hardware')
    expect(feld('Leistungsdatum / -zeitraum')).toHaveValue('01.11.2026 – 15.11.2026')
  })

  it('ändert den Titel; leer gespeichert überlässt er die Anzeige dem Server', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Titel'))
    await nutzer.type(feld('Titel'), '  Toner und Papier ')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    await nutzer.clear(feld('Titel'))
    await nutzer.tab()

    await waitFor(async () =>
      expect(
        (await anfragen()).filter((a) => a.methode === 'PUT').map((a) => (a.body as { titel: string }).titel),
      ).toEqual(['Toner und Papier', '']),
    )
  })

  it('lehnt ungültiges Datum und ungültigen Betrag ab', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Rechnungsdatum'))
    await nutzer.type(feld('Rechnungsdatum'), '31.02.2026')
    await nutzer.clear(feld('Netto Steuerzeile 1'))
    await nutzer.type(feld('Netto Steuerzeile 1'), '221.8x')
    await nutzer.tab()

    expect(status()).toHaveTextContent('Nicht gespeichert – Eingaben prüfen')
    expect(feld('Rechnungsdatum')).toHaveAttribute('aria-invalid', 'true')
    expect(feld('Rechnungsdatum')).toHaveAccessibleDescription(FEHLER_DATUM)
    expect(feld('Netto Steuerzeile 1')).toHaveAccessibleDescription(FEHLER_BETRAG)
    expect((await anfragen()).filter((a) => a.methode === 'PUT')).toHaveLength(0)

    await nutzer.clear(feld('Rechnungsdatum'))
    await nutzer.type(feld('Rechnungsdatum'), '20.11.2026')
    expect(feld('Rechnungsdatum')).not.toHaveAttribute('aria-invalid')
    await nutzer.clear(feld('Netto Steuerzeile 1'))
    await nutzer.type(feld('Netto Steuerzeile 1'), '221,80')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect((await puts(anfragen)).map((a) => a.body)).toEqual([
      expect.objectContaining({
        rechnungsdatum: '2026-11-20',
        steuerzeilen: [{ satz: 19, netto: 221.8, steuer: 42.15 }],
      }),
    ])
  })

  it('zeigt beim Auto-Save nur Fehler bereits verlassener Felder', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.click(screen.getByRole('button', { name: '+ Steuerzeile' }))
    // 19 % gibt es schon – die neue Zeile beginnt mit dem nächsten freien Satz.
    expect(screen.getByLabelText('Steuersatz Steuerzeile 2')).toHaveValue('7')
    await nutzer.click(screen.getByLabelText('Steuersatz Steuerzeile 2'))
    await nutzer.tab()
    // Netto und Steuer der neuen Zeile sind leer, aber noch nicht verlassen: kein Fehler, nur „ungespeichert“.
    expect(screen.getByLabelText('Netto Steuerzeile 2')).not.toHaveAttribute('aria-invalid')
    expect(status()).toHaveTextContent('Ungespeicherte Änderungen')
    await nutzer.type(screen.getByLabelText('Netto Steuerzeile 2'), '100,00')
    await nutzer.tab()
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '7,00')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect(await puts(anfragen)).toHaveLength(1)
    expect((await puts(anfragen))[0].body).toMatchObject({
      steuerzeilen: [
        { satz: 19, netto: 221.85, steuer: 42.15 },
        { satz: 7, netto: 100, steuer: 7 },
      ],
    })
  })

  it('meldet einen doppelten Steuersatz am Feld, statt still nicht zu speichern', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.click(screen.getByRole('button', { name: '+ Steuerzeile' }))
    await nutzer.clear(screen.getByLabelText('Steuersatz Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuersatz Steuerzeile 2'), '19')
    await nutzer.type(screen.getByLabelText('Netto Steuerzeile 2'), '100,00')
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '19,00')
    await nutzer.tab()
    expect(status()).toHaveTextContent('Nicht gespeichert – Eingaben prüfen')
    expect(screen.getByLabelText('Steuersatz Steuerzeile 2')).toHaveAccessibleDescription(fehlerSatzDoppelt(1))
    expect(await puts(anfragen)).toHaveLength(0)

    await nutzer.clear(screen.getByLabelText('Steuersatz Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuersatz Steuerzeile 2'), '7')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
  })

  it('zeigt ausgelesene negative Beträge einer Rechnung und speichert nach dem Wechsel auf Gutschrift', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2
        ? {
            ...b,
            netto: -221.85,
            steuer: -42.15,
            brutto: -264,
            steuerzeilen: [{ satz: 19, netto: -221.85, steuer: -42.15, stimmt: true }],
          }
        : b,
    )
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'G')
    await nutzer.tab()

    // Der Beleg kam so aus der Erkennung: Die Fehler stehen trotzdem sichtbar da, nicht nur ein rotes Wölkchen.
    expect(status()).toHaveTextContent('Nicht gespeichert – Eingaben prüfen')
    expect(feld('Netto Steuerzeile 1')).toHaveAccessibleDescription(FEHLER_NEGATIV)
    expect(feld('Steuer Steuerzeile 1')).toHaveAccessibleDescription(FEHLER_NEGATIV)
    expect(within(formular()).getByText(`Brutto laut Rechnung: ${FEHLER_NEGATIV}`)).toBeInTheDocument()
    expect(await puts(anfragen)).toHaveLength(0)

    await nutzer.selectOptions(feld('Belegart'), 'Gutschrift')
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect(within(formular()).queryByText(`Brutto laut Rechnung: ${FEHLER_NEGATIV}`)).not.toBeInTheDocument()
    expect((await puts(anfragen)).map((a) => a.body)).toEqual([
      expect.objectContaining({ belegart: 'Gutschrift', rechnungsnummer: '2026-48817G', brutto: -264 }),
    ])
  })

  it('zeigt Serverfehler zu Steuerzeilen, Rechnungswerten und ohne eigenes Feld', async () => {
    server.use(
      http.put('*/api/belege/:id', () =>
        problem(400, 'Die Eingabe ist ungültig.', {
          'Steuerzeilen[0].Satz': ['Der Steuersatz liegt zwischen 0 und 100 %.'],
          'steuerzeilen[0]': ['Die Zeile passt nicht.'],
          steuerzeilen: ['Jeder Steuersatz darf nur einmal vorkommen.'],
          brutto: ['Negative Beträge gibt es nur bei Gutschriften.'],
          skonto: ['Skonto passt nicht.'],
        }),
      ),
    )
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), '-1')
    await nutzer.tab()

    await waitFor(() => expect(status()).toHaveTextContent('Nicht gespeichert – Eingaben prüfen'))
    expect(feld('Steuersatz Steuerzeile 1')).toHaveAccessibleDescription('Der Steuersatz liegt zwischen 0 und 100 %.')
    const tabelle = screen.getByRole('table', { name: 'Beträge nach Steuersatz' })
    expect(within(tabelle).getByText('Steuerzeile 1: Die Zeile passt nicht.')).toBeInTheDocument()
    expect(within(formular()).getByText('Jeder Steuersatz darf nur einmal vorkommen.')).toBeInTheDocument()
    expect(
      within(formular()).getByText('Brutto laut Rechnung: Negative Beträge gibt es nur bei Gutschriften.'),
    ).toBeInTheDocument()
    expect(within(formular()).getByText('Nicht gespeichert: Skonto passt nicht.')).toBeInTheDocument()

    // Bearbeiten der Zeile nimmt ihre Meldung weg.
    await nutzer.type(feld('Netto Steuerzeile 1'), '0')
    expect(within(tabelle).queryByText('Steuerzeile 1: Die Zeile passt nicht.')).not.toBeInTheDocument()
  })

  it('zeigt Serverfehler am Feld', async () => {
    server.use(
      http.put('*/api/belege/:id', () =>
        problem(400, 'Die Eingabe ist ungültig.', { Rechnungsnummer: ['Rechnungsnummer ist bereits vergeben.'] }),
      ),
    )
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), '-1')
    await nutzer.tab()
    expect(await screen.findByText('Rechnungsnummer ist bereits vergeben.')).toBeInTheDocument()
    expect(feld('Rechnungsnummer')).toHaveAccessibleDescription('Rechnungsnummer ist bereits vergeben.')
  })

  it('zeigt beim Leeren der Kategorie den Default-Hinweis und speichert sofort', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.click(feld('Kategorie'))
    // Erneutes Wählen der aktuellen Kategorie leert das Feld.
    await nutzer.click(await screen.findByRole('option', { name: /Bürobedarf/ }))
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect((await puts(anfragen)).map((a) => a.body)).toEqual([expect.objectContaining({ kategorieId: null })])
    expect(feld('Kategorie')).toHaveTextContent('Keine Kategorie')
    expect(within(formular()).getByText('leer · Default: Bürobedarf')).toBeInTheDocument()
  })

  it('fügt eine Steuerzeile hinzu und meldet abweichende Steuer und Summen', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.click(screen.getByRole('button', { name: '+ Steuerzeile' }))
    await nutzer.clear(screen.getByLabelText('Steuersatz Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuersatz Steuerzeile 2'), '7')
    await nutzer.type(screen.getByLabelText('Netto Steuerzeile 2'), '100,00')
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '7,50')
    const zeile = screen.getByLabelText('Netto Steuerzeile 2').closest<HTMLElement>('[role="row"]')!
    expect(within(zeile).getByRole('img', { name: 'Steuer passt nicht zum Satz' })).toBeInTheDocument()
    expect(screen.getByLabelText('Steuer Steuerzeile 2')).toHaveAccessibleDescription(
      '7 % von 100,00 € wären 7,00 € – eingetragen sind 7,50 € (Differenz 0,50 €)',
    )
    // Zeile + drei Summen weichen ab.
    expect(within(formular()).getByText('4 Abweichungen')).toBeInTheDocument()
    await nutzer.clear(screen.getByLabelText('Steuer Steuerzeile 2'))
    await nutzer.type(screen.getByLabelText('Steuer Steuerzeile 2'), '7,01')
    expect(within(zeile).getByRole('img', { name: 'Steuer passt: 7 % von 100,00 € = 7,00 €' })).toBeInTheDocument()
    expect(within(formular()).getByText('3 Abweichungen')).toBeInTheDocument()

    await nutzer.click(screen.getByRole('button', { name: 'Steuerzeile 2 entfernen' }))
    expect(screen.queryByLabelText('Netto Steuerzeile 2')).not.toBeInTheDocument()
    // Verlassen der Zeile und Entfernen speichern je einmal – abwarten, damit nichts in den nächsten Test läuft.
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
  })

  it('setzt einen abweichenden Rechnungswert auf die Summe der Steuerzeilen', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Steuer Steuerzeile 1'))
    await nutzer.type(feld('Steuer Steuerzeile 1'), '40,00')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect(
      screen.getByRole('button', {
        name: 'USt weicht ab: Laut Rechnung 42,15 €, das sind 2,15 € mehr als die Summe der Steuerzeilen.',
      }),
    ).toBeInTheDocument()

    await nutzer.click(screen.getByRole('button', { name: 'Rechnungswert auf 40,00 € setzen' }))
    await waitFor(async () => expect(await puts(anfragen)).toHaveLength(2))
    expect((await puts(anfragen))[1].body).toMatchObject({ steuer: 40, netto: 221.85 })
    expect(screen.getByRole('button', { name: /^USt stimmt/ })).toBeInTheDocument()
  })

  it('zeigt ohne Steuerzeilen die Werte laut Rechnung ohne Eingabefelder', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.click(screen.getByRole('button', { name: 'Steuerzeile 1 entfernen' }))
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))

    const tabelle = screen.getByRole('table', { name: 'Beträge nach Steuersatz' })
    expect(within(tabelle).getByText('Keine Aufschlüsselung nach Steuersätzen')).toBeInTheDocument()
    const summe = within(tabelle).getByRole('rowheader', { name: 'Laut Rechnung' }).parentElement!
    expect(summe).toHaveTextContent('221,85 €42,15 €264,00 €')
    expect(within(tabelle).queryByRole('textbox')).not.toBeInTheDocument()
    expect(within(formular()).queryByText('Alles stimmig')).not.toBeInTheDocument()
  })

  it('„Rückgängig“ speichert den Stand vor der letzten Änderung wieder', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'X')
    await nutzer.tab()
    await nutzer.click(await within(formular()).findByRole('button', { name: 'Rückgängig' }))
    await waitFor(() => expect(feld('Rechnungsnummer')).toHaveValue('2026-48817'))
    await waitFor(() =>
      expect(within(formular()).queryByRole('button', { name: 'Rückgängig' })).not.toBeInTheDocument(),
    )
    expect((await puts(anfragen)).map((a) => (a.body as { rechnungsnummer: string }).rechnungsnummer)).toEqual([
      '2026-48817X',
      '2026-48817',
    ])
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
  })

  it('legt im Tag-Feld einen neuen Tag an und hängt ihn an den Beleg', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    const tags = within(formular()).getByText('Tags').nextElementSibling as HTMLElement

    await nutzer.click(within(tags).getByRole('button', { name: '+ Tag hinzufügen' }))
    await screen.findByRole('option', { name: /Reisekosten/ })
    await nutzer.type(screen.getByRole('combobox', { name: 'Tag suchen' }), 'Bewirtung{Enter}')
    await waitFor(() => expect(within(tags).getByText('Bewirtung')).toBeInTheDocument())

    const liste = await anfragen()
    expect(liste.map((a) => [a.methode, a.pfad])).toEqual([
      ['POST', '/api/tags'],
      ['POST', `/api/belege/${b2}/tags`],
    ])
    expect(liste[0].body).toMatchObject({ name: 'Bewirtung', istZahlungsTag: false })
  })

  it('speichert beim Verlassen, ohne nachzufragen', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer, router } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'X')
    const tabs = screen.getByRole('navigation', { name: 'Belegdaten' })
    await nutzer.click(within(tabs).getByRole('link', { name: /^Positionen/ }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}/positionen`))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect((await puts(anfragen)).map((a) => (a.body as { rechnungsnummer: string }).rechnungsnummer)).toEqual([
      '2026-48817X',
    ])
  })

  it('fragt beim Verlassen nach, wenn ungültige Eingaben nicht gespeichert werden können', async () => {
    const { nutzer, router } = await oeffneDaten()
    await nutzer.clear(feld('Fällig am'))
    await nutzer.type(feld('Fällig am'), '99.99.2026')
    const tabs = screen.getByRole('navigation', { name: 'Belegdaten' })
    await nutzer.click(within(tabs).getByRole('link', { name: /^Positionen/ }))
    const dialog = await screen.findByRole('alertdialog', { name: 'Änderungen nicht gespeichert' })
    expect(dialog).toHaveTextContent('einige Eingaben sind ungültig')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Weiter bearbeiten' }))
    expect(router.state.location.pathname).toBe(`/belege/${b2}`)
    expect(feld('Fällig am')).toHaveValue('99.99.2026')
    expect(feld('Fällig am')).toHaveAccessibleDescription(FEHLER_DATUM)

    await nutzer.click(within(tabs).getByRole('link', { name: /^Positionen/ }))
    const erneut = await screen.findByRole('alertdialog', { name: 'Änderungen nicht gespeichert' })
    await nutzer.click(within(erneut).getByRole('button', { name: 'Verwerfen und verlassen' }))
    await waitFor(() => expect(router.state.location.pathname).toBe(`/belege/${b2}/positionen`))
  })

  it('speichert offene Eingaben vor dem Bestätigen', async () => {
    db.belege = db.belege.map((b) =>
      b.id === b2 ? { ...b, befunde: b.befunde.filter((x) => x.ergebnis !== 'Fehlgeschlagen') } : b,
    )
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.type(feld('Rechnungsnummer'), 'X')
    await nutzer.click(screen.getByRole('button', { name: 'Bestätigen' }))
    expect(await screen.findByText('Beleg bestätigt')).toBeInTheDocument()
    expect((await anfragen()).map((a) => [a.methode, a.pfad])).toEqual([
      ['PUT', `/api/belege/${b2}`],
      ['POST', `/api/belege/${b2}/bestaetigen`],
    ])
  })

  it('bestätigt nicht, solange Eingaben ungültig sind', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    await nutzer.clear(feld('Netto Steuerzeile 1'))
    await nutzer.type(feld('Netto Steuerzeile 1'), 'x')
    await nutzer.click(screen.getByRole('button', { name: 'Bestätigen' }))
    expect(
      await screen.findByText('Nicht bestätigt: Änderungen im Tab „Daten“ sind nicht gespeichert'),
    ).toBeInTheDocument()
    expect(feld('Netto Steuerzeile 1')).toHaveAccessibleDescription(FEHLER_BETRAG)
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(await anfragen()).toEqual([])
  })

  it('ist für NurLesen schreibgeschützt und ohne Speichern-Leiste', async () => {
    szenario.rolle('NurLesen')
    await oeffneDaten()
    for (const label of ['Rechnungsnummer', 'Rechnungsdatum', 'Leistungsdatum / -zeitraum', 'Fällig am'])
      expect(feld(label)).toHaveAttribute('readonly')
    expect(within(formular()).queryByLabelText('Netto Steuerzeile 1')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Rechnungswert auf/ })).not.toBeInTheDocument()
    expect(feld('Belegart')).toBeDisabled()
    expect(feld('Korrespondent')).toBeDisabled()
    expect(feld('Kategorie')).toBeDisabled()
    expect(within(formular()).queryByRole('status')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Tag hinzufügen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Inbox entfernen' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '+ Steuerzeile' })).not.toBeInTheDocument()
    expect(screen.getByRole('table', { name: 'Beträge nach Steuersatz' })).toHaveTextContent('19 %')
  })

  it('setzt ein geändertes Feld nach Rückfrage auf den vorherigen Wert zurück', async () => {
    const { anfragen } = anfragenMitschneiden()
    const { nutzer } = await oeffneDaten()
    const zuruecksetzen = (label: string) => within(formular()).queryByRole('button', { name: `${label} zurücksetzen` })
    expect(zuruecksetzen('Rechnungsnummer')).not.toBeInTheDocument()

    await nutzer.clear(feld('Rechnungsnummer'))
    await nutzer.type(feld('Rechnungsnummer'), '2026-48818')
    await nutzer.tab()
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    expect(zuruecksetzen('Titel')).not.toBeInTheDocument()

    // Abbrechen lässt den Wert stehen.
    await nutzer.click(zuruecksetzen('Rechnungsnummer')!)
    let dialog = await screen.findByRole('alertdialog')
    expect(dialog).toHaveTextContent('Wollen Sie Rechnungsnummer wirklich auf „2026-48817“ zurücksetzen?')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Abbrechen' }))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(feld('Rechnungsnummer')).toHaveValue('2026-48818')

    await nutzer.click(zuruecksetzen('Rechnungsnummer')!)
    dialog = await screen.findByRole('alertdialog')
    await nutzer.click(within(dialog).getByRole('button', { name: 'Zurücksetzen' }))

    await waitFor(async () => expect(await puts(anfragen)).toHaveLength(2))
    expect((await puts(anfragen)).at(-1)?.body).toMatchObject({ rechnungsnummer: '2026-48817' })
    await waitFor(() => expect(feld('Rechnungsnummer')).toHaveValue('2026-48817'))
    expect(zuruecksetzen('Rechnungsnummer')).not.toBeInTheDocument()
    expect(feld('Rechnungsnummer')).toHaveFocus()
  })

  it('bietet das Zurücksetzen auch bei Auswahlfeldern an', async () => {
    const { nutzer } = await oeffneDaten()
    await nutzer.selectOptions(feld('Belegart'), 'Gutschrift')
    await waitFor(() => expect(status()).toHaveTextContent('Gespeichert'))
    await nutzer.click(within(formular()).getByRole('button', { name: 'Belegart zurücksetzen' }))
    expect(await screen.findByRole('alertdialog')).toHaveTextContent(
      'Wollen Sie Belegart wirklich auf „Rechnung“ zurücksetzen?',
    )
  })

  it('zeigt bei E-Rechnungen die Herkunft statt der Konfidenz', async () => {
    renderBelegDetail(`/belege/${belegIds.b1}`)
    await kopfGeladen('Hostwerk Süd GmbH')
    const daten = await screen.findByRole('form', { name: 'Daten' })
    expect(daten).toHaveTextContent('Aus E-Rechnung (XRechnung) übernommen')
    expect(within(daten).queryByRole('meter')).not.toBeInTheDocument()
  })
})
