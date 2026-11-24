import type { AuthStatus, Benutzer, TotpEinrichtung, Wiederherstellungscodes } from '@/api/typen'
import { benutzerIds } from './ids'

/** Angemeldeter Benutzer Martin Keller (Login.dc.html, Einstellungen → Benutzer, Benutzermenü in Topbar/Menues). */
export const angemeldeterBenutzer: Benutzer = {
  id: benutzerIds.martinKeller,
  name: 'Martin Keller',
  email: 'm.keller@firma.de',
  rolle: 'Admin',
  freigegeben: true,
  totpAktiv: true,
  anmeldung: 'Passwort',
  zuletztAktiv: '2026-11-24T09:00:00Z',
}

/** Auth-Status: eingerichtet und angemeldet; offene Registrierung und Google-Anmeldung an (Einstellungen → Benutzer). */
export const authStatus: AuthStatus = {
  eingerichtet: true,
  angemeldet: true,
  benutzer: angemeldeterBenutzer,
  registrierungOffen: true,
  googleAktiv: true,
}

/** TOTP-Einrichtung mit dem Schlüssel „JBSW Y3DP EHPK 3PXP“ aus Login.dc.html (Schritt Ersteinrichtung). */
export const totpVorschlag: TotpEinrichtung = {
  schluessel: 'JBSWY3DPEHPK3PXP',
  otpAuthUri: 'otpauth://totp/Biber-Rechnungen:m.keller%40firma.de?secret=JBSWY3DPEHPK3PXP&issuer=Biber-Rechnungen',
}

/** Zehn Wiederherstellungscodes (im Design nicht abgebildet, nur „Wiederherstellungscode verwenden“ in Login.dc.html). */
export const wiederherstellungscodes: Wiederherstellungscodes = {
  codes: [
    'K7QD-2M9X',
    'P4WF-8HTN',
    'R2ZB-6JLC',
    'V9XS-3KQE',
    'M5HT-7RDA',
    'B8NC-4WPY',
    'F3LG-9ZUV',
    'T6YJ-2BMK',
    'H1QE-5SXR',
    'D7UP-8CNW',
  ],
}

/** Zugangsdaten, mit denen sich der Mock anmelden lässt (E-Mail aus Login.dc.html). */
export const mockZugang: { email: string; passwort: string; totpCode: string; wiederherstellungscode: string } = {
  email: 'm.keller@firma.de',
  passwort: 'biber-demo',
  totpCode: '123456',
  wiederherstellungscode: 'K7QD-2M9X',
}
