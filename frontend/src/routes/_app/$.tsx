import { createFileRoute } from '@tanstack/react-router'
import { SeiteNichtGefunden } from '@/components/fehler/Fehlerseiten'

/**
 * Fängt alle unbekannten Adressen ab. Als Kind von `_app` läuft vorher die Anmeldeprüfung,
 * und die 404 erscheint mit Seitenleiste statt im Vollbild.
 */
export const Route = createFileRoute('/_app/$')({
  component: () => <SeiteNichtGefunden />,
})
