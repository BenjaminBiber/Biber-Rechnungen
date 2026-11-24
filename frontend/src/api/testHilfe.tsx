/// <reference types="node" />
import { File as NodeFile } from 'node:buffer'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { onTestFinished, vi } from 'vitest'

/**
 * Upload-Tests: jsdom bringt eigene `FormData`/`File` mit, `fetch` und `Request` stammen aber aus Node (undici)
 * und verstehen sie nicht – Multipart-Bodies kämen leer an. Setzt bis zum Testende die Node-Varianten ein;
 * Dateien danach mit `new File(…)` anlegen.
 */
export async function nodeFormDataVerwenden(): Promise<void> {
  const leer = new Request('http://localhost/', { method: 'POST', body: new URLSearchParams() })
  const NodeFormData = (await leer.formData()).constructor
  vi.stubGlobal('FormData', NodeFormData)
  vi.stubGlobal('File', NodeFile)
  onTestFinished(() => {
    vi.unstubAllGlobals()
  })
}

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
