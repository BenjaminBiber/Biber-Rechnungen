import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const backend = 'http://localhost:8080'

export default defineConfig({
  plugins: [
    // Muss vor dem React-Plugin stehen, damit routeTree.gen.ts erzeugt ist.
    tanstackRouter({ target: 'react', autoCodeSplitting: true }),
    react(),
    tailwindcss(),
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
