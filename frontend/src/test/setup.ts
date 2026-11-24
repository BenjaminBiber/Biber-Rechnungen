import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/mocks/server'

// jsdom kennt kein Scrollen; der Router stellt die Scroll-Position wieder her.
window.scrollTo = () => {}

// Jede Anfrage ohne Handler lässt den Test scheitern – so fallen fehlende Mocks sofort auf.
// (MSW 3 heißt die Option `onUnhandledFrame`, früher `onUnhandledRequest`.)
beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))

afterEach(() => {
  cleanup()
  server.resetHandlers()
  window.localStorage.clear()
  delete document.documentElement.dataset.theme
})

afterAll(() => server.close())
