import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'

/** Service Worker für `VITE_MOCKS=1` (npm run preview:mock). */
export const worker = setupWorker(...handlers)
