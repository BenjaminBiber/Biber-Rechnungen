import type { Konto } from '@/api/typen'

/** Eigenes Konto von Martin Keller (Einstellungen.dc.html → Mein Konto). */
export const konto: Konto = {
  name: 'Martin Keller',
  email: 'm.keller@firma.de',
  passwortGeaendertAm: '2026-09-02T08:30:00Z',
  totpAktiv: true,
  wiederherstellungscodes: 8,
  googleVerknuepft: false,
  aktiveSitzungen: 2,
}
