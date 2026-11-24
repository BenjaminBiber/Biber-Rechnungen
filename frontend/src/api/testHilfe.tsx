import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'

/** Rendert einen Hook mit frischem QueryClient (ohne Wiederholungen) – für Hook-Tests gegen MSW. */
export function renderMitQuery<T>(hook: () => T) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity }, mutations: { retry: false } },
  })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { ...renderHook(hook, { wrapper }), queryClient }
}

/** Wartet, bis die Query (bei jedem Aufruf von `lesen` neu gelesen) Daten hat, und liefert sie. */
export async function erwarteDaten<D>(lesen: () => { data?: D; isSuccess: boolean; isFetching?: boolean }): Promise<D> {
  await waitFor(() => {
    const query = lesen()
    if (!query.isSuccess || query.isFetching) throw new Error('Query noch nicht fertig')
  })
  return lesen().data as D
}
