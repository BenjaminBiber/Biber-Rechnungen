import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { wendeDarstellungAn } from '@/lib/theme'
import { erzeugeQueryClient, erzeugeRouter } from '@/router'
import '@/styles/index.css'

const queryClient = erzeugeQueryClient()
const router = erzeugeRouter(queryClient)

/** Startet MSW nur im Mock-Betrieb; im normalen Build wird das Modul nicht geladen. */
async function starteMocks(): Promise<void> {
  if (import.meta.env.VITE_MOCKS !== '1') return
  const { worker } = await import('@/mocks/browser')
  await worker.start({ onUnhandledFrame: 'bypass', quiet: true })
}

// Farbschema vor dem ersten Rendern setzen, damit nichts aufblitzt.
wendeDarstellungAn()

void starteMocks().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </StrictMode>,
  )
})
