import { describe, expect, it } from 'vitest'
import { standardFilter } from '@/mocks/fixtures/ansichten'
import { ausFilter, filterZurueckgesetzt, SEITENGROESSE, sucheValidieren, zuAbfrage, zuFilter } from './suche'
import { seitenListe } from './Zusammenfassung'

describe('sucheValidieren', () => {
  it('lässt Vorgaben und ungültige Werte weg', () => {
    expect(
      sucheValidieren({
        ansicht: 'Alle',
        darstellung: 'tabelle',
        sortierung: 'Rechnungsdatum',
        absteigend: true,
        seite: 1,
        zahlstatus: 'Irgendwas',
        quellen: ['Mail', 'Fax'],
        von: '1.1.2026',
        betragVon: 'abc',
      }),
    ).toEqual({ quellen: ['Mail'] })
  })

  it('übernimmt gültige Werte, auch als Zeichenketten', () => {
    expect(
      sucheValidieren({ ansicht: 'ZuPruefen', seite: '3', absteigend: 'false', betragBis: '99.5', tags: 'a' }),
    ).toEqual({ ansicht: 'ZuPruefen', seite: 3, absteigend: false, betragBis: 99.5, tags: ['a'] })
  })
})

describe('zuAbfrage', () => {
  it('setzt die Seitengröße und benennt die Listen wie der Vertrag', () => {
    expect(zuAbfrage({ tags: ['t'], korrespondenten: ['k'], kategorien: ['c'], seite: 2 })).toEqual({
      tagIds: ['t'],
      korrespondentIds: ['k'],
      kategorieIds: ['c'],
      seite: 2,
      seitengroesse: SEITENGROESSE,
    })
  })

  it('schickt Suchfeld und Betragsart nur mit Suche bzw. Betrag', () => {
    expect(zuAbfrage({ suchfeld: 'Rechnungsnummer', betragArt: 'Netto' })).toEqual({ seitengroesse: SEITENGROESSE })
    expect(zuAbfrage({ suche: 'x', suchfeld: 'Rechnungsnummer', betragVon: 1, betragArt: 'Netto' })).toMatchObject({
      suchfeld: 'Rechnungsnummer',
      betragArt: 'Netto',
    })
  })
})

describe('gespeicherte Ansichten', () => {
  it('zuFilter und ausFilter sind zueinander passend', () => {
    const suche = { ansicht: 'Offen' as const, kategorien: ['c'], sortierung: 'Brutto' as const, absteigend: false }
    const filter = zuFilter(suche)
    expect(filter).toEqual({
      ...standardFilter,
      ansicht: 'Offen',
      kategorieIds: ['c'],
      sortierung: 'Brutto',
      absteigend: false,
      seitengroesse: SEITENGROESSE,
    })
    expect(ausFilter(filter, 'klein')).toEqual({ ...suche, darstellung: 'klein' })
  })

  it('Filter zurücksetzen behält Darstellung und Sortierung', () => {
    expect(filterZurueckgesetzt({ darstellung: 'gross', sortierung: 'Brutto', tags: ['t'], seite: 4 })).toEqual({
      darstellung: 'gross',
      sortierung: 'Brutto',
    })
  })
})

describe('seitenListe', () => {
  it('zeigt Auslassungen wie im Design', () => {
    expect(seitenListe(1, 21)).toEqual([1, 2, 3, '…', 21])
    expect(seitenListe(10, 21)).toEqual([1, '…', 9, 10, 11, '…', 21])
    expect(seitenListe(21, 21)).toEqual([1, '…', 19, 20, 21])
    expect(seitenListe(4, 21)).toEqual([1, 2, 3, 4, 5, '…', 21])
    expect(seitenListe(1, 1)).toEqual([1])
  })
})
