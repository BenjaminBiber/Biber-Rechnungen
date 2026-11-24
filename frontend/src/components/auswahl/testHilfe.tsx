import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'

/** Rendert eine Auswahl-Komponente mit frischem QueryClient gegen die MSW-Handler. */
export function renderAuswahl(ui: ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  })
  const nutzer = userEvent.setup()
  const ergebnis = render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
  return { ...ergebnis, nutzer, queryClient }
}
