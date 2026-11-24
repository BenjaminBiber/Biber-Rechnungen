import { createHmac } from 'node:crypto'

const BASE32 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'

function base32Dekodieren(text: string): Buffer {
  const sauber = text.replace(/[\s=]/g, '').toUpperCase()
  let bits = ''
  for (const zeichen of sauber) {
    const wert = BASE32.indexOf(zeichen)
    if (wert < 0) throw new Error(`Ungültiges Base32-Zeichen „${zeichen}“`)
    bits += wert.toString(2).padStart(5, '0')
  }
  const bytes = bits.match(/.{8}/g) ?? []
  return Buffer.from(bytes.map((b) => parseInt(b, 2)))
}

/** TOTP-Code nach RFC 6238 (SHA-1, 30 s, 6 Ziffern) – wie eine Authenticator-App. */
export function totpCode(schluessel: string, zeitpunkt = Date.now()): string {
  const zaehler = Buffer.alloc(8)
  zaehler.writeBigUInt64BE(BigInt(Math.floor(zeitpunkt / 1000 / 30)))
  const hmac = createHmac('sha1', base32Dekodieren(schluessel)).update(zaehler).digest()
  const versatz = hmac[hmac.length - 1] & 0x0f
  const zahl = (hmac.readUInt32BE(versatz) & 0x7fffffff) % 1_000_000
  return String(zahl).padStart(6, '0')
}

/**
 * Wartet, bis der aktuelle 30-s-Zeitschritt noch mindestens `reserveSekunden` läuft, und liefert dann den Code –
 * so verfällt der Code nicht zwischen Berechnen und Absenden.
 */
export async function frischerTotpCode(schluessel: string, reserveSekunden = 5): Promise<string> {
  const rest = 30 - ((Date.now() / 1000) % 30)
  if (rest < reserveSekunden) await new Promise((r) => setTimeout(r, rest * 1000 + 200))
  return totpCode(schluessel)
}
