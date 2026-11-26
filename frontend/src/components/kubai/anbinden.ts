import { DUR, go, mountIcon, runtime, springe, type KubaiIconDef } from './engine.js'

/**
 * Wann ein Icon seine Bewegung spielt:
 * - `auto`: richtet sich nach der Umgebung (siehe {@link binde}).
 * - `hover`: Mauszeiger oder Tastaturfokus auf dem umgebenden Bedienelement.
 * - `klick`: beim Auslösen des umgebenden Bedienelements, als Rückmeldung, dass etwas passiert ist.
 * - `aus`: nie.
 */
export type IconAusloeser = 'auto' | 'hover' | 'klick' | 'aus'

// Die Icons sind einfarbig (currentColor); die Engine braucht trotzdem eine Palette.
runtime.palette = { name: 'Schrift', colors: ['#2E3036'] }

const BEDIENELEMENT =
  'button, a, summary, label, [role="button"], [role="menuitem"], [role="menuitemcheckbox"], [role="menuitemradio"], [role="option"], [role="tab"]'

let naechsteId = 0

/**
 * Baut das Icon in `host` auf und verbindet es mit dem nächsten umgebenden Bedienelement.
 *
 * Bei `auto` entscheidet die Umgebung: In einem Bedienelement mit sichtbarem Text reagiert das Icon auf Hover und
 * Tastaturfokus. Steht es allein (Icon-Button), spielt es beim Klick – dort sagt die Bewegung, dass der Klick
 * angekommen ist. Ohne Bedienelement bleibt es ruhig. Mit `auftritt` (das Icon hat gerade ein anderes abgelöst)
 * spielt es einmal, egal wo es steht.
 *
 * Liefert die Aufräumfunktion.
 */
export function binde(host: HTMLElement, def: KubaiIconDef, ausloeser: IconAusloeser, auftritt: boolean): () => void {
  const inst = mountIcon(def, 0, host, `s${naechsteId++}`)
  const ziel = host.closest<HTMLElement>(BEDIENELEMENT)
  const modus = ausloeser !== 'auto' ? ausloeser : !ziel ? 'aus' : hatSichtbarenText(ziel) ? 'hover' : 'klick'
  const quelle = ziel ?? host
  let zeiger = false
  let fokus = false
  let uhr = 0

  const zeige = (hover: number) => {
    if (hover === inst.hover) return
    inst.hover = hover
    go(inst)
  }
  /** Einmal abspielen: hin und – wenn das Icon nicht von selbst im Ruhebild endet – wieder zurück. */
  const spiele = () => {
    window.clearTimeout(uhr)
    if (def.einmal) {
      // Immer von vorn, auch wenn die letzte Runde noch läuft.
      inst.hover = 0
      springe(inst)
    }
    inst.hover = 1
    go(inst)
    uhr = window.setTimeout(() => {
      if (def.einmal) {
        inst.hover = 0
        springe(inst)
      } else if (!zeiger && !fokus) {
        inst.hover = 0
        go(inst)
      }
    }, DUR)
  }
  const halte = () => {
    if (def.einmal) {
      // Endet im Ruhebild: beim Betreten einmal spielen, beim Verlassen nichts zurückspulen.
      if (zeiger || fokus) spiele()
      return
    }
    window.clearTimeout(uhr)
    zeige(Number(zeiger || fokus))
  }

  const zeigerAn = (e: PointerEvent) => {
    if (e.pointerType !== 'mouse' || zeiger) return
    zeiger = true
    halte()
  }
  const zeigerAus = (e: PointerEvent) => {
    if (e.pointerType !== 'mouse') return
    zeiger = false
    halte()
  }
  // Nur Tastaturfokus zählt; ein Mausklick fokussiert das Element auch, soll das Icon aber nicht festhalten.
  const fokusAn = () => {
    const vorher = fokus
    fokus = sichtbarerFokus(quelle)
    if (fokus !== vorher) halte()
  }
  const fokusAus = () => {
    if (!fokus) return
    fokus = false
    halte()
  }
  // Auf Touch-Geräten gibt es kein Hover; dort antwortet das Icon auf das Antippen.
  const klick = () => {
    if (modus === 'hover' && (zeiger || fokus)) return
    spiele()
  }

  if (modus === 'hover') {
    quelle.addEventListener('pointerenter', zeigerAn)
    quelle.addEventListener('pointerleave', zeigerAus)
    quelle.addEventListener('focus', fokusAn)
    quelle.addEventListener('blur', fokusAus)
  }
  if (modus === 'hover' || modus === 'klick') quelle.addEventListener('click', klick)

  if (auftritt && modus !== 'aus') {
    if (def.einmal) spiele()
    else {
      // Aus der bewegten Lage ins Ruhebild einlaufen.
      inst.hover = 1
      springe(inst)
      inst.hover = 0
      go(inst)
    }
  }

  return () => {
    window.clearTimeout(uhr)
    cancelAnimationFrame(inst.raf)
    quelle.removeEventListener('pointerenter', zeigerAn)
    quelle.removeEventListener('pointerleave', zeigerAus)
    quelle.removeEventListener('focus', fokusAn)
    quelle.removeEventListener('blur', fokusAus)
    quelle.removeEventListener('click', klick)
    host.replaceChildren()
  }
}

/** Hat das Bedienelement Text, den man sieht? Nur für Screenreader gedachter Text (`sr-only`) zählt nicht. */
function hatSichtbarenText(el: HTMLElement): boolean {
  const lauf = document.createTreeWalker(el, NodeFilter.SHOW_TEXT)
  for (let knoten = lauf.nextNode(); knoten; knoten = lauf.nextNode()) {
    if (!knoten.textContent?.trim()) continue
    const eltern = knoten.parentElement
    if (eltern?.closest('.sr-only, .material-symbols-outlined')) continue
    return true
  }
  return false
}

function sichtbarerFokus(el: HTMLElement): boolean {
  try {
    return el.matches(':focus-visible')
  } catch {
    return true
  }
}
