import { setupWorker } from 'msw/browser'
import { handlers } from './handlers'
import { szenario } from './szenarien'

/** Service Worker für `VITE_MOCKS=1` (npm run preview:mock). */
export const worker = setupWorker(...handlers)

/**
 * Startzustand für Playwright: `localStorage['mock-szenario']` = `abgemeldet` oder `nichtEingerichtet`
 * (per `page.addInitScript` vor dem Laden setzen). Der Zustand lebt im Seitenkontext und gilt bis zum Neuladen.
 */
const start = typeof localStorage === 'undefined' ? null : localStorage.getItem('mock-szenario')
if (start === 'abgemeldet') szenario.abgemeldet()
if (start === 'nichtEingerichtet') szenario.nichtEingerichtet()
