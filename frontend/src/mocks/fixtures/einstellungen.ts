import type { Einstellungen, IntegritaetsLauf, Loeschbar, MailStatus, Verfahrensdoku } from '@/api/typen'
import { integritaetsLaufId, tagIds } from './ids'

/** Alle Einstellungsgruppen aus den Bereichen von Einstellungen.dc.html (Allgemein bis Benutzer & Freigaben). */
export const einstellungen: Einstellungen = {
  allgemein: {
    firmenname: 'Musterfirma GmbH',
    standardwaehrung: 'EUR',
    zeitzone: 'Europe/Berlin',
    inboxTagId: tagIds.inbox,
  },
  ust: {
    status: 'Unbekannt',
    zeitraum: 'Quartalsweise',
  },
  pruefungen: {
    aktiv: {
      Positionssumme: true,
      NettoSteuerBrutto: true,
      Pflichtfelder: true,
      Datum: true,
      Paragraph14: true,
      Dublette: true,
      NeuerKorrespondent: true,
      Konfidenz: true,
      Kosit: true,
    },
    summenToleranz: 0.02,
    maxAlterJahre: 2,
    kleinbetragGrenze: 250,
    konfidenzSchwelle: 0.8,
  },
  di: {
    monatslimit: 500,
    warnungAbProzent: 80,
    maxSeitenJeBeleg: 10,
  },
  mail: {
    aktiv: true,
    abrufintervallMinuten: 5,
    eingangsordner: 'INBOX',
    verarbeitetOrdner: 'Verarbeitet',
    ohneBelegOrdner: 'Ohne Beleg',
  },
  ordner: {
    pollingSekunden: 30,
    stabilSekunden: 10,
  },
  aufbewahrung: {
    jahre: 8,
  },
  zugriff: {
    offeneRegistrierung: true,
    googleAnmeldung: true,
  },
  umgebung: {
    ablagePfad: '/srv/rechnungen/ablage',
    eingangPfad: '/srv/rechnungen/eingang',
    oeffentlicheUrl: 'https://rechnungen.<domain>',
    diEndpunkt: 'https://…westeurope.cognitiveservices.azure.com',
    diModell: 'prebuilt-invoice',
    imapServer: 'imap.example-mail.de:993',
    imapBenutzer: 'rechnungen@firma.de',
    diKonfiguriert: true,
    imapKonfiguriert: true,
    googleKonfiguriert: true,
  },
}

/** Status des E-Mail-Imports: „verbunden · vor 3 Min.“ (Einstellungen.dc.html → E-Mail-Import). */
export const mailStatus: MailStatus = {
  verbunden: true,
  letzterAbruf: '2026-11-24T08:57:00Z',
  fehler: null,
}

/** Löschbare Belegjahre: leer, „Keine Belege mit abgelaufener Frist“ (Einstellungen.dc.html → Aufbewahrung). */
export const loeschbar: Loeschbar[] = []

/** Integritätsläufe 23.–21.11.2026, jeweils 03:00 Ortszeit (Einstellungen.dc.html → Integrität & Sicherung). */
export const integritaetsLaeufe: IntegritaetsLauf[] = [
  {
    id: integritaetsLaufId(1),
    gestartetAm: '2026-11-23T02:00:00Z',
    dauerSekunden: 240,
    anzahlDateien: 2184,
    anzahlAbweichungen: 0,
    abweichungen: [],
  },
  {
    id: integritaetsLaufId(2),
    gestartetAm: '2026-11-22T02:00:00Z',
    dauerSekunden: 238,
    anzahlDateien: 2179,
    anzahlAbweichungen: 0,
    abweichungen: [],
  },
  {
    id: integritaetsLaufId(3),
    gestartetAm: '2026-11-21T02:00:00Z',
    dauerSekunden: 236,
    anzahlDateien: 2171,
    anzahlAbweichungen: 0,
    abweichungen: [],
  },
]

/** Stand der Verfahrensdokumentation: Version 0.1 (Entwurf), Stand 23.11.2026, v0.4.0 (Einstellungen.dc.html → Verfahrensdoku). */
export const verfahrensdoku: Verfahrensdoku = {
  version: '0.1',
  stand: '2026-11-23',
  offeneStellen: 3,
  softwareVersion: 'v0.4.0',
}
