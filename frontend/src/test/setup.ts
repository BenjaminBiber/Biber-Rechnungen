/// <reference types="node" />
import { File as NodeFile } from 'node:buffer'
import '@testing-library/jest-dom/vitest'
// jsdom kennt keine IndexedDB – die Upload-Warteschlange speichert dort.
import 'fake-indexeddb/auto'
import { cleanup, configure } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from '@/mocks/server'

// jsdom bringt eigene `FormData`/`File` mit, `fetch` und `Request` stammen aber aus Node (undici) und
// verstehen sie nicht – Multipart-Bodies kämen leer an. Deshalb gelten in allen Tests die Node-Varianten.
// Direkt zuweisen statt `vi.stubGlobal`, damit `vi.unstubAllGlobals()` in einzelnen Tests sie nicht zurücksetzt.
const leererBody = new Request('http://localhost/', { method: 'POST', body: new URLSearchParams() })
globalThis.FormData = (await leererBody.formData()).constructor as typeof FormData
globalThis.File = NodeFile as unknown as typeof File

// `findBy…`/`waitFor` warten standardmäßig nur 1 s – unter Last zu knapp.
configure({ asyncUtilTimeout: 3000 })

// jsdom kennt kein Scrollen; der Router stellt die Scroll-Position wieder her.
window.scrollTo = () => {}

// jsdom kennt keine Blob-URLs; Downloads (Export) legen eine an.
URL.createObjectURL ??= () => 'blob:test'
URL.revokeObjectURL ??= () => {}

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
