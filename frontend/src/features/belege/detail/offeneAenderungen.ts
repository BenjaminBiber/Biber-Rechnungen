/**
 * Offene Eingaben im Tab „Daten“: Das Formular meldet sich hier mit seiner Speicherfunktion an, damit Aktionen im Kopf
 * („Bestätigen“, „Erneut auslesen“) sie vorher speichern, statt sie stillschweigend zu übergehen.
 */
const speicherer = new Map<string, () => Promise<boolean>>()

/** Meldet die Speicherfunktion für den Beleg an; die Rückgabe meldet sie wieder ab. */
export function aenderungenAnmelden(belegId: string, speichern: () => Promise<boolean>): () => void {
  speicherer.set(belegId, speichern)
  return () => {
    if (speicherer.get(belegId) === speichern) speicherer.delete(belegId)
  }
}

/** Speichert offene Eingaben des Belegs. `false`, wenn sie ungültig sind oder das Speichern fehlschlug. */
export function aenderungenSpeichern(belegId: string): Promise<boolean> {
  return speicherer.get(belegId)?.() ?? Promise.resolve(true)
}
