import type { Benutzer } from '@/api/typen'
import { angemeldeterBenutzer } from './auth'
import { benutzerIds } from './ids'

/** Benutzer aus Einstellungen.dc.html → Benutzer & Freigaben; Jonas Wagner wartet auf Freigabe. */
export const benutzerListe: Benutzer[] = [
  angemeldeterBenutzer,
  {
    id: benutzerIds.benutzer2,
    name: 'Anna Lehmann',
    email: 'a.lehmann@firma.de',
    rolle: 'Benutzer',
    freigegeben: true,
    totpAktiv: false,
    anmeldung: 'Google',
    zuletztAktiv: '2026-11-20T15:42:00Z',
  },
  {
    id: benutzerIds.benutzer3,
    name: 'Steuerbüro',
    email: 'kanzlei@hofmann-partner.de',
    rolle: 'NurLesen',
    freigegeben: true,
    totpAktiv: true,
    anmeldung: 'Passwort',
    zuletztAktiv: '2026-10-05T09:15:00Z',
  },
  {
    id: benutzerIds.benutzer4,
    name: 'Jonas Wagner',
    email: 'j.wagner@firma.de',
    rolle: null,
    freigegeben: false,
    totpAktiv: false,
    anmeldung: 'Passwort',
    zuletztAktiv: null,
  },
]
