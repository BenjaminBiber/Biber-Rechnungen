/// <reference types="vite-plugin-pwa/client" />
import { hinzufuegen as toastZeigen } from '@/components/toast/toast-store'
import { warteschlange } from '@/features/hochladen/uploadWarteschlange'

/**
 * Registriert den PWA-Service-Worker und startet die Upload-Warteschlange. Im Mock-Betrieb
 * (`VITE_MOCKS=1`) gehört der Service-Worker-Platz MSW – dann wird nichts registriert.
 */
export async function pwaRegistrieren(): Promise<void> {
  warteschlange.starten()
  if (import.meta.env.VITE_MOCKS === '1' || !('serviceWorker' in navigator)) return
  const { registerSW } = await import('virtual:pwa-register')
  const aktualisieren = registerSW({
    onNeedRefresh() {
      toastZeigen('info', 'Neue Version verfügbar', {
        text: 'Aktualisieren',
        ausfuehren: () => void aktualisieren(true),
      })
    },
  })
}
