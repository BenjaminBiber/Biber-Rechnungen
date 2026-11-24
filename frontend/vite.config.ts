import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

const backend = 'http://localhost:8080'

/** Dateitypen, die das Share-Target aus anderen Apps annimmt (wie die Dropzone). */
const geteilteTypen = ['application/pdf', 'text/xml', 'application/xml', 'image/png', 'image/jpeg', 'image/tiff']

export default defineConfig({
  plugins: [
    // Muss vor dem React-Plugin stehen, damit routeTree.gen.ts erzeugt ist.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    VitePWA({
      // In Vitest gibt es keinen Service Worker.
      disable: !!process.env.VITEST,
      strategies: 'injectManifest',
      srcDir: 'src',
      filename: 'sw.ts',
      registerType: 'prompt',
      // Registriert wird in src/pwa/registrieren.ts – nur ohne Mock-Server.
      injectRegister: false,
      injectManifest: {
        // woff2 genügt allen unterstützten Browsern; die woff-Varianten blähen nur den Precache auf.
        globPatterns: ['**/*.{js,css,html,woff2,png,svg,ico,webmanifest}'],
        globIgnores: ['mockServiceWorker.js'],
        maximumFileSizeToCacheInBytes: 8 * 1024 * 1024,
      },
      manifest: {
        name: 'Biber-Rechnungen',
        short_name: 'Belege',
        description: 'Belegarchiv für Eingangsrechnungen',
        lang: 'de',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        theme_color: '#2F2D86',
        background_color: '#F4F5F7',
        icons: [
          { src: '/icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/icons/icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
        share_target: {
          action: '/geteilt',
          method: 'POST',
          enctype: 'multipart/form-data',
          params: { files: [{ name: 'dateien', accept: geteilteTypen }] },
        },
      },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': backend,
      '/mcp': backend,
      '/health': backend,
    },
  },
  build: {
    outDir: 'dist',
  },
})
