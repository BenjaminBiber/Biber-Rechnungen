import { istApiFehler } from '@/api/client'

/** Verständliche Meldung zu einem Fehler beim Erzeugen (409 mit `pfade`) oder Herunterladen (410) eines Exports. */
export function exportFehlertext(fehler: unknown, bezeichnung: string): string {
  if (istApiFehler(fehler) && fehler.status === 409) {
    const pfade = Array.isArray(fehler.problem.pfade) ? fehler.problem.pfade.filter((p) => typeof p === 'string') : []
    return pfade.length
      ? `Export ${bezeichnung} nicht erstellt: Originale seit dem Import verändert – ${pfade.join(', ')}`
      : `Export ${bezeichnung} nicht erstellt: ${fehler.message}`
  }
  if (istApiFehler(fehler) && fehler.status === 410)
    return `Die Exportdatei von ${bezeichnung} fehlt auf dem Server. Bitte den Export neu erstellen.`
  return `Export ${bezeichnung} konnte nicht erstellt werden.`
}
