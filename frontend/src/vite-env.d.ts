/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** „1“ startet den Mock-Service-Worker (npm run preview:mock). */
  readonly VITE_MOCKS?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
