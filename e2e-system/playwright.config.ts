import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-End-Tests gegen das echte System (Docker Compose, siehe start.sh).
 * Die Tests bauen aufeinander auf (01 richtet das System ein, spätere nutzen dessen Belege) –
 * deshalb ein Worker, feste Reihenfolge über die Dateinamen und kein Wiederholen.
 */
export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: !!process.env.CI,
  timeout: 120_000,
  expect: { timeout: 30_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    baseURL: process.env.E2E_BASIS_URL ?? 'http://localhost:8080',
    viewport: { width: 1440, height: 900 },
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    acceptDownloads: true,
  },
})
