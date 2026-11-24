/** Erlaubte Endungen – gleich dem `accept` der Dropzone. */
export const erlaubteEndungen = ['.pdf', '.xml', '.png', '.jpg', '.jpeg', '.tif', '.tiff'] as const

export const maxGroesse = 50 * 1024 * 1024

export const grundFormat = 'Format nicht unterstützt. Erlaubt sind PDF, XML, PNG, JPG und TIFF.'
export const grundGroesse = 'Datei ist größer als 50 MB.'

/** Grund, warum eine Datei nicht gesendet wird – `null`, wenn sie hochgeladen werden darf. */
export function ablehnungsgrund(datei: { name: string; size: number }): string | null {
  const name = datei.name.toLowerCase()
  if (!erlaubteEndungen.some((endung) => name.endsWith(endung))) return grundFormat
  if (datei.size > maxGroesse) return grundGroesse
  return null
}

/** Dateigröße wie im Design: „284 KB“, „2,1 MB“. */
export function formatGroesse(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / 1024 / 1024).toLocaleString('de-DE', { maximumFractionDigits: 1 })} MB`
}
