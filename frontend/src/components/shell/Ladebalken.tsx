import { useIsFetching } from '@tanstack/react-query'
import { useRouterState } from '@tanstack/react-router'

/**
 * Schmaler Balken an der Unterkante der Kopfleiste, solange ein Seitenwechsel lädt oder Daten zum ersten Mal kommen.
 * Neu-Laden im Hintergrund (Fokus, Polling) zählt nicht – die Seite zeigt dann ja schon Inhalt.
 * Erscheint erst nach 250 ms (CSS), damit schnelle Antworten nicht blinken. Der Elternknoten muss positioniert sein.
 */
export function Ladebalken() {
  const seiteLaedt = useRouterState({ select: (s) => s.status === 'pending' })
  const ersteDaten = useIsFetching({ predicate: (q) => q.state.data === undefined })
  const aktiv = seiteLaedt || ersteDaten > 0
  return <div className="ladebalken" data-aktiv={aktiv} data-testid="ladebalken" aria-hidden="true" />
}
