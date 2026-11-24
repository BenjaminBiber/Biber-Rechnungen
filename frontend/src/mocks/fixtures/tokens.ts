import type { ApiToken } from '@/api/typen'
import { tokenId } from './ids'

/** API-Tokens aus Einstellungen.dc.html → API-Tokens (MCP); „Test“ ist widerrufen. */
export const apiTokens: ApiToken[] = [
  {
    id: tokenId(1),
    name: 'Claude Desktop',
    endung: '7f3a',
    scopes: ['lesen', 'upload'],
    erstelltAm: '2026-09-02T09:10:00Z',
    zuletztGenutztAm: '2026-11-21T13:20:00Z',
    widerrufenAm: null,
  },
  {
    id: tokenId(2),
    name: 'Claude Code (Laptop)',
    endung: 'c019',
    scopes: ['lesen'],
    erstelltAm: '2026-10-15T14:35:00Z',
    zuletztGenutztAm: '2026-11-22T16:02:00Z',
    widerrufenAm: null,
  },
  {
    id: tokenId(3),
    name: 'Test',
    endung: '9921',
    scopes: ['lesen'],
    erstelltAm: '2026-08-01T10:00:00Z',
    zuletztGenutztAm: '2026-08-03T11:30:00Z',
    widerrufenAm: '2026-08-03T12:00:00Z',
  },
]

/** Wählbare Scopes beim Erstellen eines Tokens (Einstellungen.dc.html, Dialoge.dc.html „API-Token erstellt“). */
export const tokenScopes: string[] = ['lesen', 'upload']
