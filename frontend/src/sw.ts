/// <reference lib="webworker" />
import { CacheableResponsePlugin } from 'workbox-cacheable-response'
import { clientsClaim } from 'workbox-core'
import { ExpirationPlugin } from 'workbox-expiration'
import { cleanupOutdatedCaches, createHandlerBoundToURL, precacheAndRoute } from 'workbox-precaching'
import { NavigationRoute, registerRoute } from 'workbox-routing'
import { CacheFirst, NetworkFirst, StaleWhileRevalidate } from 'workbox-strategies'
import { ablehnungsgrund } from './features/hochladen/pruefung'
import { ablegen, alleWartendenSenden, syncTag } from './pwa/warteschlange'
import { apiCaches, cacheNamen, istListe, istVorschau, istEinzelbeleg } from './pwa/cacheRegeln'

/**
 * Service Worker der PWA (vite-plugin-pwa, Modus `injectManifest`). Nur relative Importe – der
 * Service Worker wird getrennt von der App gebaut.
 */
declare const self: ServiceWorkerGlobalScope & { __WB_MANIFEST: (string | { url: string; revision: string | null })[] }

// Beim ersten Aktivieren sofort die offene Seite übernehmen, damit schon ihre Listen im Cache landen.
// Updates warten trotzdem auf „Aktualisieren“ (registerType: prompt → skipWaiting erst auf Nachricht).
clientsClaim()

// App-Shell: alle Build-Dateien inkl. Schrift und Icons.
precacheAndRoute(self.__WB_MANIFEST)
cleanupOutdatedCaches()

// Navigationen → index.html aus dem Cache (außer API, MCP und Health-Check).
registerRoute(
  new NavigationRoute(createHandlerBoundToURL('index.html'), {
    denylist: [/^\/api(\/|$)/, /^\/mcp(\/|$)/, /^\/health(\/|$)/],
  }),
)

const nurOk = new CacheableResponsePlugin({ statuses: [200] })

// Listen: sofort aus dem Cache, im Hintergrund aktualisieren.
registerRoute(
  ({ url, request }) => request.method === 'GET' && url.origin === self.location.origin && istListe(url.pathname),
  new StaleWhileRevalidate({
    cacheName: cacheNamen.listen,
    plugins: [nurOk, new ExpirationPlugin({ maxEntries: 60, maxAgeSeconds: 7 * 24 * 60 * 60 })],
  }),
  'GET',
)

// Einzelne Belege: Netz zuerst, nach 3 s der zuletzt geladene Stand.
registerRoute(
  ({ url, request }) => request.method === 'GET' && url.origin === self.location.origin && istEinzelbeleg(url.pathname),
  new NetworkFirst({
    cacheName: cacheNamen.belege,
    networkTimeoutSeconds: 3,
    plugins: [nurOk, new ExpirationPlugin({ maxEntries: 100, maxAgeSeconds: 7 * 24 * 60 * 60 })],
  }),
  'GET',
)

// Vorschaubilder ändern sich nicht – Cache zuerst.
registerRoute(
  ({ url, request }) => request.method === 'GET' && url.origin === self.location.origin && istVorschau(url.pathname),
  new CacheFirst({
    cacheName: cacheNamen.vorschau,
    plugins: [nurOk, new ExpirationPlugin({ maxEntries: 200, maxAgeSeconds: 30 * 24 * 60 * 60 })],
  }),
  'GET',
)

/** Leert alle API-Caches (Abmelden). Der Precache der App-Shell bleibt. */
async function apiCachesLeeren(): Promise<void> {
  await Promise.all(apiCaches.map((name) => caches.delete(name)))
}

async function anAlleFenster(nachricht: { typ: string; anzahl?: number }): Promise<void> {
  for (const fenster of await self.clients.matchAll({ type: 'window', includeUncontrolled: true }))
    fenster.postMessage(nachricht)
}

/** Share-Target: geteilte Dateien in die Warteschlange legen und zur Hochladen-Seite umleiten. */
async function geteiltAnnehmen(anfrage: Request): Promise<Response> {
  let anzahl = 0
  try {
    const formular = await anfrage.formData()
    for (const wert of formular.getAll('dateien')) {
      if (typeof wert === 'string' || ablehnungsgrund(wert)) continue
      await ablegen(wert, 'geteilt')
      anzahl++
    }
  } catch {
    // Kein lesbares Formular – trotzdem zur Seite führen.
  }
  if (anzahl) {
    void anAlleFenster({ typ: 'warteschlange-geaendert' })
    void hintergrundSyncAnmelden()
  }
  return Response.redirect(`/hochladen?geteilt=${anzahl}`, 303)
}

async function hintergrundSyncAnmelden(): Promise<void> {
  const sync = (self.registration as ServiceWorkerRegistration & { sync?: { register(tag: string): Promise<void> } })
    .sync
  await sync?.register(syncTag).catch(() => {})
}

self.addEventListener('fetch', (ereignis) => {
  const url = new URL(ereignis.request.url)
  if (url.origin !== self.location.origin) return
  if (ereignis.request.method === 'POST' && url.pathname === '/geteilt') {
    ereignis.respondWith(geteiltAnnehmen(ereignis.request))
    return
  }
  // Abmelden: Anfrage ganz normal durchreichen, danach die API-Caches leeren.
  if (ereignis.request.method === 'POST' && url.pathname === '/api/auth/abmelden') {
    ereignis.respondWith(
      fetch(ereignis.request).then(async (antwort) => {
        await apiCachesLeeren()
        return antwort
      }),
    )
  }
})

/** Background Sync: alle wartenden Uploads senden. Wirft bei fehlendem Netz – der Browser versucht es erneut. */
async function nachsenden(): Promise<void> {
  try {
    const { nichtAngemeldet } = await alleWartendenSenden()
    if (nichtAngemeldet) await anAlleFenster({ typ: 'anmeldung-noetig', anzahl: nichtAngemeldet })
  } finally {
    await anAlleFenster({ typ: 'warteschlange-geaendert' })
  }
}

self.addEventListener('sync', (ereignis) => {
  const sync = ereignis as ExtendableEvent & { tag: string }
  if (sync.tag === syncTag) sync.waitUntil(nachsenden())
})

self.addEventListener('message', (ereignis) => {
  const daten = ereignis.data as { type?: string; typ?: string } | undefined
  // Von virtual:pwa-register („Aktualisieren“ im Toast).
  if (daten?.type === 'SKIP_WAITING') void self.skipWaiting()
  if (daten?.typ === 'api-caches-leeren') ereignis.waitUntil(apiCachesLeeren())
})
