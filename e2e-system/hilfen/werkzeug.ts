import { execFileSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const composeSkript = join(dirname(fileURLToPath(import.meta.url)), '..', 'compose.sh')

/** `docker compose …` des E2E-Systems; liefert stdout als Buffer. */
export function compose(argumente: string[], eingabe?: Buffer): Buffer {
  return execFileSync('bash', [composeSkript, ...argumente], {
    input: eingabe,
    maxBuffer: 64 * 1024 * 1024,
    stdio: ['pipe', 'pipe', 'pipe'],
  })
}

/** Befehl im Hilfscontainer `werkzeug` (Eingang unter /eingang, Ablage unter /ablage). */
function werkzeug(befehl: string, eingabe?: Buffer, benutzer = '1000:1000'): Buffer {
  return compose(['exec', '-T', '-u', benutzer, 'werkzeug', 'sh', '-c', befehl], eingabe)
}

function inAnfuehrung(text: string): string {
  return `'${text.replace(/'/g, `'\\''`)}'`
}

export function sha256(inhalt: Buffer): string {
  return createHash('sha256').update(inhalt).digest('hex')
}

/** Legt eine Datei in den Eingangsordner (als Benutzer der API, wie ein Scanner über Samba). */
export function eingangAblegen(dateiname: string, inhalt: Buffer): void {
  werkzeug(`cat > /eingang/${inAnfuehrung(dateiname)}`, inhalt)
}

/** Dateien im Eingangsordner (ohne Unterordner wie `_abgelehnt`). */
export function eingangDateien(): string[] {
  // BusyBox-find kennt kein -printf.
  return werkzeug('cd /eingang && find . -maxdepth 1 -type f | sed "s|^\\./||"').toString('utf8').split('\n').filter(Boolean)
}

/** Alle Dateien der Ablage, relativ zu ihrem Wurzelordner (`2026/Korrespondent/…`). */
export function ablageDateien(): string[] {
  return werkzeug('cd /ablage && find . -type f | sed "s|^\\./||"').toString('utf8').split('\n').filter(Boolean)
}

export function ablageLesen(relativerPfad: string): Buffer {
  return werkzeug(`cat /ablage/${inAnfuehrung(relativerPfad)}`)
}

/** Verändert eine Datei der Ablage (als root, die Ablage ist schreibgeschützt) – simuliert Bitfäule. */
export function ablageVeraendern(relativerPfad: string): void {
  werkzeug(`chmod u+w /ablage/${inAnfuehrung(relativerPfad)} && printf 'manipuliert' >> /ablage/${inAnfuehrung(relativerPfad)}`, undefined, '0')
}
