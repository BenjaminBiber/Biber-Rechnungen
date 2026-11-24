import { setupServer } from 'msw/node'
import { handlers } from './handlers'

/** MSW-Server für Vitest (siehe src/test/setup.ts). */
export const server = setupServer(...handlers)
