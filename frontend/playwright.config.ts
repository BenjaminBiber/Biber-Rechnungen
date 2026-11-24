import { defineConfig, devices } from '@playwright/test'

const basisUrl = 'http://localhost:4173'
/** Build ohne Mocks mit PWA-Service-Worker; API-Antworten liefert der Test per `context.route`. */
const pwaUrl = 'http://localhost:4174'

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
      command: 'npm run preview:mock',
      url: basisUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
    {
      command: 'npm run preview:pwa',
      url: pwaUrl,
      reuseExistingServer: !process.env.CI,
      timeout: 180_000,
    },
  ],
})
