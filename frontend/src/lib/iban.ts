/** IBAN ohne Leerzeichen, in Großbuchstaben. */
function normalisieren(iban: string): string {
  return iban.replace(/\s+/g, '').toUpperCase()
}

/** Prüft Aufbau und Prüfziffer (ISO 13616, Modulo 97). */
export function ibanGueltig(iban: string): boolean {
  const wert = normalisieren(iban)
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(wert)) return false
  const umgestellt = wert.slice(4) + wert.slice(0, 4)
  let rest = 0
  for (const zeichen of umgestellt) {
    const ziffern = /\d/.test(zeichen) ? zeichen : String(zeichen.charCodeAt(0) - 55)
    for (const ziffer of ziffern) rest = (rest * 10 + Number(ziffer)) % 97
  }
  return rest === 1
}

/** Vierergruppen, z. B. „DE89 3704 0044 0532 0130 00“. */
export function ibanFormatieren(iban: string): string {
  return normalisieren(iban).replace(/(.{4})(?=.)/g, '$1 ')
}

/** BIC ohne Leerzeichen, in Großbuchstaben. */
export function bicNormalisieren(bic: string): string {
  return normalisieren(bic)
}

/** Prüft den Aufbau (ISO 9362): 8 oder 11 Stellen. */
export function bicGueltig(bic: string): boolean {
  return /^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$/.test(normalisieren(bic))
}
