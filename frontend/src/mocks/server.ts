import { setupServer } from 'msw/node'
import { afterEach } from 'vitest'
import { zuruecksetzen } from './db'
import { handlers } from './handlers'

/** MSW-Server für Vitest (siehe src/test/setup.ts). */
export const server = setupServer(...handlers)

// Jeder Test beginnt mit den Beispieldaten des Designs – Änderungen und Szenarien gelten nur im eigenen Test.
afterEach(() => zuruecksetzen())
