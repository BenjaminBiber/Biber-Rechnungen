import { cpSync, createReadStream, existsSync, statSync } from 'node:fs'
import { join, normalize, resolve, sep } from 'node:path'
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

const backend = 'http://localhost:8080'

/** Hilfsdateien von pdf.js, die es zur Laufzeit nachlädt (siehe PdfSeite.tsx). */
const pdfjsVerzeichnisse = ['wasm', 'cmaps', 'standard_fonts', 'iccs']

/**
 * Liefert die pdf.js-Hilfsdateien unter `/pdfjs/…` aus: im Dev-Server direkt aus node_modules, im Build
 * nach `dist/pdfjs/` kopiert. Ohne `wasm/openjpeg.wasm` fehlen z. B. JPEG2000-Bilder (oft Briefköpfe).
 */
function pdfjsDateien(): Plugin {
  const quelle = resolve(fileURLToPath(new URL('.', import.meta.url)), 'node_modules/pdfjs-dist')
  let ausgabe = ''
  return {
    name: 'pdfjs-dateien',
    configResolved(config) {
      ausgabe = resolve(config.root, config.build.outDir)
    },
    configureServer(server) {
      server.middlewares.use('/pdfjs', (req, res, next) => {
        const pfad = normalize(join(quelle, decodeURIComponent((req.url ?? '').split('?')[0])))
        // `sep` statt '/', sonst greift die Prüfung unter Windows (Backslash-Pfade) nie.
        const erlaubt = pdfjsVerzeichnisse.some((v) => pfad.startsWith(join(quelle, v) + sep))
        if (!erlaubt || !existsSync(pfad) || !statSync(pfad).isFile()) return next()
        if (pfad.endsWith('.wasm')) res.setHeader('Content-Type', 'application/wasm')
        createReadStream(pfad).pipe(res)
      })
    },
    closeBundle() {
      for (const v of pdfjsVerzeichnisse) cpSync(join(quelle, v), join(ausgabe, 'pdfjs', v), { recursive: true })
    },
  }
}

/** Dateitypen, die das Share-Target aus anderen Apps annimmt (wie die Dropzone). */
const geteilteTypen = ['application/pdf', 'text/xml', 'application/xml', 'image/png', 'image/jpeg', 'image/tiff']

export default defineConfig({
  plugins: [
    // Muss vor dem React-Plugin stehen, damit routeTree.gen.ts erzeugt ist.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
    pdfjsDateien(),
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
