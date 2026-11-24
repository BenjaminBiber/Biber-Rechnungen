import { defineConfig, devices } from '@playwright/test'

/**
 * Ports je Worktree: aus `E2E_PORT`/`E2E_PWA_PORT`, sonst deterministisch aus dem Arbeitsordner abgeleitet.
 * Playwright lädt die Config in jedem Worker neu – deshalb zurück in `process.env` schreiben, damit alle
 * Prozesse denselben Port sehen. Parallele Worktrees bekommen so verschiedene Ports.
 */
function ordnerPort(): number {
  let hash = 0
  for (const zeichen of process.cwd().toLowerCase()) hash = (hash * 31 + zeichen.charCodeAt(0)) >>> 0
  return 4200 + (hash % 350) * 2
}
process.env.E2E_PORT ??= String(ordnerPort())
process.env.E2E_PWA_PORT ??= String(Number(process.env.E2E_PORT) + 1)
const port = Number(process.env.E2E_PORT)
const pwaPort = Number(process.env.E2E_PWA_PORT)

const basisUrl = `http://localhost:${port}`
/** Build ohne Mocks mit PWA-Service-Worker; API-Antworten liefert der Test per `context.route`. */
const pwaUrl = `http://localhost:${pwaPort}`

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? 'list' : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: basisUrl,
    locale: 'de-DE',
    timezoneId: 'Europe/Berlin',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'desktop',
      testIgnore: 'pwa.spec.ts',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    {
      name: 'mobil',
      testIgnore: 'pwa.spec.ts',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 390, height: 844 },
        isMobile: true,
        hasTouch: true,
        deviceScaleFactor: 3,
      },
    },
    {
      name: 'pwa',
      testMatch: 'pwa.spec.ts',
      use: {
        ...devices['Desktop Chrome'],
        baseURL: pwaUrl,
        viewport: { width: 390, height: 844 },
        hasTouch: true,
        serviceWorkers: 'allow',
      },
    },
  ],
  webServer: [
    {
      // Nie einen laufenden Server übernehmen: er könnte aus einem anderen Worktree stammen.
      // Mit `--strictPort` scheitert ein belegter Port laut.
      command: `npm run preview:mock -- --port ${port}`,
      url: basisUrl,
      reuseExistingServer: false,
      timeout: 180_000,
    },
    {
      command: `npm run preview:pwa -- --port ${pwaPort}`,
      url: pwaUrl,
      reuseExistingServer: false,
      timeout: 180_000,
    },
  ],
})
