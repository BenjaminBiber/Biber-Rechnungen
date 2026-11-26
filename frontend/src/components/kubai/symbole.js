// Copyright (c) 2026 Kubai087. Licensed under the Kubai Icons License; see LICENSE.
// Alle übrigen Icons der Anwendung, im selben Stil wie die Hauptnavigation: einfarbige Linien-Icons
// im 24er-Raster der Material Symbols (Strich 1.8, currentColor), bewegt mit der Kubai-Engine.
// Der Schlüssel ist der Ligaturname des Material-Symbols, das das Icon ersetzt.
//
// Jedes Icon hat genau zwei Zustände (Ruhe, Hover) und einen Fortschritt o von 0 bis 1. Die meisten
// Bewegungen lassen sich je Teil (`data-n`) mit ein paar Zahlen beschreiben:
//   x, y, r, s      linear: Verschiebung, Drehung (Grad), Skalierung bei o = 1
//   dreh            volle Drehung, die wieder im Ruhebild endet (360, oder die Symmetrie des Icons)
//   wackel, k       Drehung, die k-mal ausschwingt und gerade stehen bleibt
//   pendelX/Y, k    dasselbe als Verschiebung (Kopfschütteln, Scanlinie)
//   puls, pulsY     kurzes Anschwellen der Skalierung, endet bei 1
//   huepf           kurzer Hüpfer nach oben, endet bei 0
//   kipp            Münzwurf um die senkrechte Achse
//   o: [x, y]       Drehpunkt (Standard: Mitte)
// Endet jede Bewegung wieder im Ruhebild, ist das Icon `einmal`: der Auslöser spielt es einmal
// vorwärts ab, statt es beim Verlassen rückwärts laufen zu lassen.
import { rp, rr, at, f } from './engine.js'
import { NAV_ICONS } from './navIcons.js'

const VIEWBOX = '0 0 24 24'
const LINIE = 'fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"'

const n_ = (n) => (n ? ` data-n="${n}"` : '')
const P = (d, n) => `<path${n_(n)} d="${d}"/>`
const G = (n, ...teile) => `<g${n_(n)}>${teile.join('')}</g>`
const C = (cx, cy, r, n) => `<circle${n_(n)} cx="${cx}" cy="${cy}" r="${r}"/>`
// Gefüllter Punkt.
const D = (cx, cy, r = 1.3, n) => `<circle${n_(n)} cx="${cx}" cy="${cy}" r="${r}" fill="currentColor" stroke="none"/>`
// Gefüllte Fläche (der Strich rundet die Ecken).
const F = (d, n) => `<path${n_(n)} d="${d}" fill="currentColor" stroke-width="1"/>`
// Fläche, die `.icon-gefuellt` am Icon leicht einfärbt.
const K = (markup) => markup.replace(/^<(\w+)/, '<$1 class="koerper"')
// Durchgestrichen: der Strich bekommt links und rechts eine Lücke aus dem Motiv geschnitten.
const durch = (u, inhalt, d = 'M4 4L20 20') => `<mask id="${u.id('d')}" maskUnits="userSpaceOnUse" x="0" y="0" width="24" height="24">
    <rect width="24" height="24" fill="#fff" stroke="none"/><path d="${d}" stroke="#000" stroke-width="4.8"/></mask>
  <g mask="url(#${u.id('d')})">${inhalt}</g><path d="${d}"/>`

const LINEAR = ['x', 'y', 'r', 's']

function bewege(el, m, o) {
  const [ox, oy] = m.o || [12, 12]
  const bogen = Math.sin(Math.PI * o)
  const ausklang = (1 - o) * Math.sin((m.k || 2) * Math.PI * o)
  const x = (m.x || 0) * o + (m.pendelX || 0) * ausklang
  const y = (m.y || 0) * o + (m.pendelY || 0) * ausklang - (m.huepf || 0) * bogen
  const r = ((m.r || 0) + (m.dreh || 0)) * o + (m.wackel || 0) * ausklang
  let sx = 1 + ((m.s ?? 1) - 1) * o + (m.puls || 0) * bogen
  const sy = sx + (m.pulsY || 0) * bogen
  if (m.kipp) sx *= Math.abs(Math.cos(Math.PI * o))
  at(el, { transform: `translate(${f(x + ox)} ${f(y + oy)}) rotate(${f(r)}) scale(${f(sx)} ${f(sy)}) translate(${-ox} ${-oy})` })
}

export const SYMBOLE = {}

// body: SVG-Teile (oder Funktion von u), bewegung: { teil: {...} }, opt.draw(o, E): eigene Formänderung,
// opt.einmal: Bewegung endet im Ruhebild (wird sonst aus der Bewegung abgeleitet).
function sym(name, body, bewegung = {}, opt = {}) {
  const teile = Object.keys(bewegung)
  const einmal = opt.einmal ?? (!opt.draw && teile.length > 0 && teile.every((t) => LINEAR.every((k) => bewegung[t][k] === undefined)))
  SYMBOLE[name] = {
    name,
    viewBox: VIEWBOX,
    mono: true,
    einmal,
    svg: (u) => `<g ${LINIE}>${typeof body === 'function' ? body(u) : body}</g>`,
    target: (h) => ({ o: h ? 1 : 0 }),
    draw(p, E) {
      for (const t of teile) bewege(E[t], bewegung[t], p.o)
      if (opt.draw) opt.draw(p.o, E)
    }
  }
}
const alias = (name, von) => { SYMBOLE[name] = { ...SYMBOLE[von], name } }
const ausNav = (name, nav) => { SYMBOLE[name] = { ...NAV_ICONS.find((i) => i.name === nav), name } }

// ---------- Aus der Hauptnavigation ----------
ausNav('space_dashboard', 'Dashboard')
ausNav('dashboard', 'Dashboard')
ausNav('receipt_long', 'Belege')
ausNav('upload_file', 'Hochladen')
ausNav('storefront', 'Korrespondenten')
ausNav('inventory_2', 'Artikel')
ausNav('sell', 'Tags')
ausNav('download', 'Export')
ausNav('settings', 'Einstellungen')

// ---------- Pfeile und Richtungen: sie rücken ein Stück in ihre Richtung ----------
sym('chevron_right', G('a', P('M9.5 6L15.5 12L9.5 18')), { a: { x: 2 } })
sym('chevron_left', G('a', P('M14.5 6L8.5 12L14.5 18')), { a: { x: -2 } })
sym('expand_more', G('a', P('M6 9.5L12 15.5L18 9.5')), { a: { y: 2 } })
sym('expand_less', G('a', P('M6 14.5L12 8.5L18 14.5')), { a: { y: -2 } })
sym('arrow_drop_down', G('a', F('M8 10.5H16L12 15Z')), { a: { y: 1.6 } })
sym('arrow_back', G('a', P('M20 12H5M11 6L5 12L11 18')), { a: { x: -2 } })
sym('arrow_upward', G('a', P('M12 20V5M6 11L12 5L18 11')), { a: { y: -2 } })
sym('arrow_downward', G('a', P('M12 4V19M6 13L12 19L18 13')), { a: { y: 2 } })
sym('first_page', P('M6 6V18') + G('a', P('M17 6L11 12L17 18')), { a: { x: -2 } })
sym('last_page', P('M18 6V18') + G('a', P('M7 6L13 12L7 18')), { a: { x: 2 } })
sym('keyboard_double_arrow_left', G('a', P('M11 6L5 12L11 18')) + G('b', P('M18 6L12 12L18 18')), { a: { x: -1.5 }, b: { x: -2.5 } })
sym('keyboard_double_arrow_right', G('a', P('M13 6L19 12L13 18')) + G('b', P('M6 6L12 12L6 18')), { a: { x: 1.5 }, b: { x: 2.5 } })
sym('swap_horiz', G('a', P('M4 8H19M16 5L19 8L16 11')) + G('b', P('M20 16H5M8 13L5 16L8 19')), { a: { x: 1.5 }, b: { x: -1.5 } })
sym('swap_vert', G('a', P('M8 20V5M5 8L8 5L11 8')) + G('b', P('M16 4V19M13 16L16 19L19 16')), { a: { y: -1.5 }, b: { y: 1.5 } })
sym('trending_up', G('a', P('M3 17L9.5 10.5L13.5 14.5L21 7M15.5 7H21V12.5')), { a: { x: 1.2, y: -1.4 } })
sym('trending_down', G('a', P('M3 7L9.5 13.5L13.5 9.5L21 17M15.5 17H21V11.5')), { a: { x: 1.2, y: 1.4 } })
sym('open_in_new', P('M11 5H7A2 2 0 0 0 5 7V17A2 2 0 0 0 7 19H17A2 2 0 0 0 19 17V13') + G('a', P('M12 12L20 4M14.5 4H20V9.5')), { a: { x: 1.5, y: -1.5 } })
sym('open_in_full', G('a', P('M14 4H20V10M20 4L13.5 10.5')) + G('b', P('M10 20H4V14M4 20L10.5 13.5')), { a: { x: 1.2, y: -1.2 }, b: { x: -1.2, y: 1.2 } })
sym('fit_screen', P('M3 9V7A2 2 0 0 1 5 5H8M16 5H19A2 2 0 0 1 21 7V9M21 15V17A2 2 0 0 1 19 19H16M8 19H5A2 2 0 0 1 3 17V15') + G('a', P(rr(8, 9.5, 8, 5, 1))), { a: { s: 1.3 } })
sym('logout', P('M10 4H6A2 2 0 0 0 4 6V18A2 2 0 0 0 6 20H10') + G('a', P('M10 12H20M16 8L20 12L16 16')), { a: { x: 2 } })
sym('input', P('M4 8V6A2 2 0 0 1 6 4H18A2 2 0 0 1 20 6V18A2 2 0 0 1 18 20H6A2 2 0 0 1 4 18V16') + G('a', P('M2.5 12H13M9.5 8.5L13 12L9.5 15.5')), { a: { x: 2.2 } })
sym('call_split', P('M12 21V13') + G('a', P('M12 13L6 7M6 11.5V7H10.5')) + G('b', P('M12 13L18 7M13.5 7H18V11.5')), { a: { r: -9, o: [12, 13] }, b: { r: 9, o: [12, 13] } })
sym('merge', G('p', P('M12 13V4M8.5 7.5L12 4L15.5 7.5')) + G('a', P('M12 13L6.5 20')) + G('b', P('M12 13L17.5 20')), { a: { r: 10, o: [12, 13] }, b: { r: -10, o: [12, 13] } })
sym('play_arrow', G('a', P(rp([[8, 5], [19, 12], [8, 19]], 1.5))), { a: { x: 1.6 } })
sym('code', G('a', P('M8.5 7L3.5 12L8.5 17')) + G('b', P('M15.5 7L20.5 12L15.5 17')), { a: { x: -1.3 }, b: { x: 1.3 } })

// ---------- Drehen ----------
const KREISPFEIL = P('M20 12A8 8 0 1 1 12 4C14.2 4 16.3 4.9 17.8 6.4L20 8.5M20 4V8.5H15.5')
sym('refresh', G('a', KREISPFEIL), { a: { dreh: 360 } })
sym('rotate_right', G('a', KREISPFEIL), { a: { r: 90 } })
sym('history', G('a', P('M4 12A8 8 0 1 0 12 4C9.8 4 7.7 4.9 6.2 6.4L4 8.5M4 4V8.5H8.5')) + P('M12 8V12L15 14'), { a: { dreh: -360 } })
sym('progress_activity', P('M12 3A9 9 0 1 0 21 12'))
sym('close', G('a', P('M6 6L18 18M18 6L6 18')), { a: { dreh: 90 } })
sym('add', G('a', P('M12 5V19M5 12H19')), { a: { dreh: 90 } })
sym('remove', G('a', P('M5 12H19')), { a: { puls: -0.25 } })
sym('check', G('a', P('M5 12.5L10 17.5L19 7.5')), { a: { puls: 0.2 } })
sym('hourglass_top', G('a', P('M6 3H18M6 21H18M7.5 3V7L12 12L16.5 7V3M7.5 21V17L12 12L16.5 17V21')), { a: { dreh: 180 } })
sym('percent', G('a', P('M18.5 5.5L5.5 18.5') + C(7, 7, 2.3) + C(17, 17, 2.3)), { a: { dreh: 180 } })
sym('grid_view', G('a', P(rr(4, 4, 6.5, 6.5, 1.2)) + P(rr(13.5, 4, 6.5, 6.5, 1.2)) + P(rr(4, 13.5, 6.5, 6.5, 1.2)) + P(rr(13.5, 13.5, 6.5, 6.5, 1.2))), { a: { dreh: 90 } })
sym('light_mode', C(12, 12, 4) + G('a', P('M12 2.5V4.5M12 19.5V21.5M2.5 12H4.5M19.5 12H21.5M5.3 5.3L6.7 6.7M17.3 17.3L18.7 18.7M5.3 18.7L6.7 17.3M17.3 6.7L18.7 5.3')), { a: { dreh: 45 } })
sym('dark_mode', G('a', P('M20 14.5A8.5 8.5 0 1 1 9.5 4A6.8 6.8 0 0 0 20 14.5Z')), { a: { wackel: -18 } })
sym('contrast', C(12, 12, 9) + G('a', '<path d="M12 3A9 9 0 0 1 12 21Z" fill="currentColor" stroke="none"/>'), { a: { r: 180 } })

// ---------- Suchen und Zoomen ----------
const LUPE = C(10.5, 10.5, 6.5) + P('M15.3 15.3L20.5 20.5')
sym('search', G('a', LUPE), { a: { wackel: 16 } })
sym('search_off', G('a', LUPE + P('M8.5 8.5L12.5 12.5M12.5 8.5L8.5 12.5')), { a: { pendelX: 1.6, k: 3 } })
sym('zoom_in', G('a', LUPE + P('M10.5 8V13M8 10.5H13')), { a: { s: 1.14, o: [14, 14] } })
sym('zoom_out', G('a', LUPE + P('M8 10.5H13')), { a: { s: 0.86, o: [10, 10] } })

// ---------- Übertragen ----------
sym('upload', P('M4 15V18A2 2 0 0 0 6 20H18A2 2 0 0 0 20 18V15') + G('a', P('M12 15V5M7.5 9.5L12 5L16.5 9.5')), { a: { y: -1.8 } })
const WOLKE = 'M17.5 19H9A7 7 0 1 1 15.71 10H17.5A4.5 4.5 0 1 1 17.5 19Z'
sym('cloud', G('a', P(WOLKE)), { a: { huepf: 1.4 } })
sym('cloud_done', P(WOLKE) + G('a', P('M8.5 13.5L11 16L15.5 11.5')), { a: { puls: 0.25, o: [12, 14] } })
sym('cloud_off', (u) => G('a', durch(u, P(WOLKE))), { a: { pendelX: 1.5, k: 3 } })
sym('cloud_upload', P('M4 14.9A7 7 0 1 1 15.71 8H17.5A4.5 4.5 0 0 1 20 16.24') + G('a', P('M12 13V21M8 17L12 13L16 17')), { a: { y: -1.6 } })
alias('backup', 'cloud_upload')
sym('link', G('a', P('M10 13A5 5 0 0 0 17.54 13.54L20.54 10.54A5 5 0 0 0 13.47 3.47L11.75 5.18')) + G('b', P('M14 11A5 5 0 0 0 6.46 10.46L3.46 13.46A5 5 0 0 0 10.53 20.53L12.24 18.82')), { a: { x: -1, y: 1 }, b: { x: 1, y: -1 } })

// ---------- Bearbeiten ----------
// Kopieren: die beiden Blätter rücken auseinander, als entstünde gerade das zweite.
sym('content_copy', G('a', P('M6.5 15.5H6A2 2 0 0 1 4 13.5V6A2 2 0 0 1 6 4H13.5A2 2 0 0 1 15.5 6V6.5')) + G('b', P(rr(9, 9, 11, 11, 2))), { a: { x: -1.1, y: -1.1 }, b: { x: 1.1, y: 1.1 } })
sym('content_cut', G('a', C(6, 18, 2.6) + P('M8 16L20 4')) + G('b', C(6, 6, 2.6) + P('M8 8L20 20')), { a: { wackel: 11, o: [12, 12] }, b: { wackel: -11, o: [12, 12] } })
const TONNE = (inhalt) => P('M6 7V19A2 2 0 0 0 8 21H16A2 2 0 0 0 18 19V7') + P(inhalt) + G('a', P('M4 7H20M9 7V5A1.5 1.5 0 0 1 10.5 3.5H13.5A1.5 1.5 0 0 1 15 5V7'))
sym('delete', TONNE('M10 11V17M14 11V17'), { a: { y: -1.6, r: -9, o: [5, 7] } })
sym('delete_forever', TONNE('M9.5 11.5L14.5 16.5M14.5 11.5L9.5 16.5'), { a: { y: -1.6, r: -9, o: [5, 7] } })
sym('edit', G('a', P('M4 20L5.2 15.3L16.3 4.2A2.12 2.12 0 0 1 19.3 7.2L8.2 18.3Z') + P('M14 6.5L17 9.5')), { a: { wackel: -9, o: [4, 20] } })
const STIFT_KLEIN = P('M12.5 21L13.2 18L19 12.2A1.6 1.6 0 0 1 21.3 14.5L15.5 20.3Z')
sym('edit_note', P('M4 7H15M4 12H11M4 17H8') + G('a', STIFT_KLEIN), { a: { wackel: -10, o: [12.5, 21] } })
sym('edit_document', P('M19 9V8L14 3H7A2 2 0 0 0 5 5V19A2 2 0 0 0 7 21H9.5') + P('M14 3V7A1 1 0 0 0 15 8H19') + G('a', STIFT_KLEIN), { a: { wackel: -10, o: [12.5, 21] } })
sym('save', G('a', P('M5 3H16L21 8V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3Z') + P('M7 3V8H15V3') + P('M7 21V14H17V21')), { a: { huepf: -1.4 } })
sym('crop', G('a', P('M6.5 2.5V15.5A2 2 0 0 0 8.5 17.5H21.5')) + G('b', P('M2.5 6.5H15.5A2 2 0 0 1 17.5 8.5V21.5')), { a: { x: 0.9, y: -0.9 }, b: { x: -0.9, y: 0.9 } })
sym('filter_alt', G('a', P('M4 5H20L14 12.5V18.5L10 20V12.5Z')), { a: { huepf: -1.2 } })
sym('tune', P('', 'l1') + C(15, 7, 2.3, 'k1') + P('', 'l2') + C(9, 17, 2.3, 'k2'), {}, {
  // Zwei Schieberegler; die Knöpfe tauschen die Seite.
  draw(o, E) {
    const regler = (linie, knopf, x, y) => {
      at(linie, { d: `M4 ${y}H${f(x - 2.3)}M${f(x + 2.3)} ${y}H20` })
      at(knopf, { cx: f(x) })
    }
    regler(E.l1, E.k1, 15 - 6 * o, 7)
    regler(E.l2, E.k2, 9 + 6 * o, 17)
  }
})

// ---------- Dateien und Ordner ----------
const BLATT = P('M6 2H14L20 8V20A2 2 0 0 1 18 22H6A2 2 0 0 1 4 20V4A2 2 0 0 1 6 2Z') + P('M14 2V7A1 1 0 0 0 15 8H20')
sym('description', BLATT + P('M8 13H16') + P('', 'z'), {}, {
  // Die zweite Zeile schreibt sich weiter.
  draw(o, E) { at(E.z, { d: `M8 17H${f(11 + 5 * o)}` }) }
})
sym('picture_as_pdf', BLATT + G('a', P(rr(7.5, 12, 9, 5.5, 1.2))), { a: { puls: 0.14, o: [12, 14.75] } })
sym('document_scanner', P('M3 8V5A2 2 0 0 1 5 3H8M16 3H19A2 2 0 0 1 21 5V8M21 16V19A2 2 0 0 1 19 21H16M8 21H5A2 2 0 0 1 3 19V16') + P(rr(8, 7, 8, 10, 1)) + G('a', P('M5.5 12H18.5')), { a: { pendelY: 4.5, k: 2 } })
sym('fact_check', P(rr(3, 4, 18, 16, 2)) + P('M14.5 9.5H17.5M14.5 14.5H17.5') + G('a', P('M6.5 12L8.7 14.2L12 10')), { a: { puls: 0.25, o: [9.2, 12] } })
sym('inventory', P(rr(5, 4.5, 14, 17, 2)) + P(rr(9, 2.5, 6, 4, 1)) + G('a', P('M9 14L11.2 16.2L15 12')), { a: { puls: 0.25, o: [12, 14] } })
const ORDNER = 'M3 6.5A2 2 0 0 1 5 4.5H9.5L12 7H19A2 2 0 0 1 21 9V17.5A2 2 0 0 1 19 19.5H5A2 2 0 0 1 3 17.5Z'
sym('folder', G('a', P(ORDNER)), { a: { huepf: 1.4 } })
sym('folder_open', P('M3 17.5V6.5A2 2 0 0 1 5 4.5H9.5L12 7H18A2 2 0 0 1 20 9V10.5') + P('', 'klappe'), {}, {
  // Die Vorderklappe kippt weiter auf.
  draw(o, E) {
    const k = 2.2 + 1.3 * o, y = 10.5 + 1.2 * o
    at(E.klappe, { d: `M3.5 19.5L${f(3.5 + k)} ${f(y + 1)}A1.5 1.5 0 0 1 ${f(5 + k)} ${f(y)}H${f(19.8 + k * 0.55)}A1 1 0 0 1 ${f(20.7 + k * 0.55)} ${f(y + 1.4)}L19 18.5A1.5 1.5 0 0 1 17.6 19.5Z` })
  }
})
sym('folder_copy', G('a', P('M3 7.5V18A2 2 0 0 0 5 20H18')) + G('b', P('M7 5.5A1.5 1.5 0 0 1 8.5 4H12L14 6H19.5A1.5 1.5 0 0 1 21 7.5V14.5A1.5 1.5 0 0 1 19.5 16H8.5A1.5 1.5 0 0 1 7 14.5Z')), { a: { x: -0.8, y: 0.8 }, b: { x: 0.8, y: -0.8 } })
sym('inbox', P('M3 13L5.5 5.5A2 2 0 0 1 7.4 4H16.6A2 2 0 0 1 18.5 5.5L21 13V18A2 2 0 0 1 19 20H5A2 2 0 0 1 3 18Z') + G('a', P('M3 13H8L9.5 15.5H14.5L16 13H21')), { a: { huepf: -1.3 } })
sym('image', P(rr(3, 4, 18, 16, 2)) + P('M21 15.5L16.5 11L6 20') + G('a', C(8.5, 9.5, 1.7)), { a: { x: 1, y: -1 } })
sym('photo_camera', P('M3 9A2 2 0 0 1 5 7H7.5L9 4.5H15L16.5 7H19A2 2 0 0 1 21 9V18A2 2 0 0 1 19 20H5A2 2 0 0 1 3 18Z') + G('a', C(12, 13, 3.6)), { a: { puls: -0.3, o: [12, 13] } })
sym('database', G('a', P('M4 6.5C4 4.6 7.6 3 12 3S20 4.6 20 6.5S16.4 10 12 10S4 8.4 4 6.5Z') + P('M4 6.5V17.5C4 19.4 7.6 21 12 21S20 19.4 20 17.5V6.5') + P('M4 12C4 13.9 7.6 15.5 12 15.5S20 13.9 20 12')), { a: { huepf: 1.3 } })
sym('dns', P(rr(3, 4, 18, 7, 1.5)) + P(rr(3, 13, 18, 7, 1.5)) + G('a', D(7, 7.5, 1.2)) + G('b', D(7, 16.5, 1.2)), { a: { puls: 0.7, o: [7, 7.5] }, b: { puls: 0.7, o: [7, 16.5] } })

// ---------- Schlösser, Schlüssel, Schutz ----------
const SCHLOSS = K(P(rr(5, 10.5, 14, 10.5, 2.2))) + D(12, 15.7, 1.4) + P('', 'buegel')
sym('lock', SCHLOSS, {}, {
  // Der Bügel hebt sich; ein Schenkel bleibt im Schloss, der andere kommt frei.
  draw(o, E) {
    const d = 1.9 * o
    at(E.buegel, { d: `M8 10.5V${f(7.5 - d)}A4 4 0 0 1 16 ${f(7.5 - d)}V${f(10.5 - d)}` })
  }
})
sym('lock_clock', P('M11.5 20.5H6.5A2 2 0 0 1 4.5 18.5V11.5A2 2 0 0 1 6.5 9.5H15.5') + P('M7.5 9.5V7A3.5 3.5 0 0 1 14.5 7V9.5') + C(17, 17, 4.5) + G('a', P('M17 14.8V17L18.6 18')), { a: { dreh: 360, o: [17, 17] } })
sym('phonelink_lock', P(rr(6, 2.5, 12, 19, 2.5)) + G('a', P(rr(9, 11, 6, 5, 1)) + P('M10.3 11V9.7A1.7 1.7 0 0 1 13.7 9.7V11')), { a: { puls: 0.2, o: [12, 12.5] } })
sym('key', G('a', C(7.5, 15.5, 4) + P('M10.4 12.6L20 3M16.5 6.5L19 9M13.5 9.5L15.5 11.5')), { a: { wackel: 16, o: [7.5, 15.5] } })
const SCHILD = K(P('M12 2.8L19.5 5.5V11.5C19.5 16 16.5 19.5 12 21.2C7.5 19.5 4.5 16 4.5 11.5V5.5Z'))
sym('verified_user', SCHILD + G('a', P('M8.5 12L11 14.5L15.5 9.5')), { a: { puls: 0.25 } })
sym('gpp_bad', G('a', SCHILD + P('M9.5 9.5L14.5 14.5M14.5 9.5L9.5 14.5')), { a: { pendelX: 1.5, k: 3 } })

// ---------- Zeit und Kalender ----------
sym('schedule', C(12, 12, 9) + G('h', P('M12 12L15.5 14')) + G('m', P('M12 12V7')), { h: { r: 8 }, m: { r: 90 } })
sym('timer', C(12, 13.5, 7.5) + P('M10 2.5H14') + G('a', P('M12 13.5V9.5')), { a: { dreh: 360, o: [12, 13.5] } })
const KALENDER = P(rr(4, 5, 16, 16, 2)) + P('M4 10H20')
const RINGE = { ringe: { huepf: 1.1 } }
const RING = G('ringe', P('M8 3V7M16 3V7'))
sym('calendar_today', KALENDER + RING + G('a', D(12, 15.5, 1.5)), { ...RINGE, a: { puls: 0.5, o: [12, 15.5] } })
sym('calendar_month', KALENDER + RING + G('a', D(8.5, 14, 1.1) + D(12, 14, 1.1) + D(15.5, 14, 1.1) + D(8.5, 17.5, 1.1) + D(12, 17.5, 1.1)), { ...RINGE, a: { puls: 0.14, o: [12, 15.75] } })
sym('event', KALENDER + RING + G('a', P(rr(12.5, 13.5, 4, 4, 0.8))), { ...RINGE, a: { puls: 0.3, o: [14.5, 15.5] } })
sym('event_busy', KALENDER + RING + G('a', P('M9.5 13L14.5 18M14.5 13L9.5 18')), { ...RINGE, a: { pendelX: 1.4, k: 3 } })

// ---------- Mitteilungen ----------
sym('notifications', G('a', P('M6.5 10.5A5.5 5.5 0 0 1 17.5 10.5V15L19.5 18H4.5L6.5 15Z') + P('M12 5V3.5') + P('M10 20.8A2 2 0 0 0 14 20.8')), { a: { wackel: 15, k: 3, o: [12, 3.5] } })
sym('mail', P(rr(3, 5, 18, 14, 2)) + P('', 'klappe'), {}, {
  // Die Klappe hebt sich.
  draw(o, E) { at(E.klappe, { d: `M3.5 7L12 ${f(13 - 3.6 * o)}L20.5 7` }) }
})
sym('alternate_email', G('a', '<g transform="translate(1.2 1.2) scale(0.9)" stroke-width="2"><circle cx="12" cy="12" r="4"/><path d="M16 8V13A3 3 0 0 0 22 13V12A10 10 0 1 0 18 20"/></g>'), { a: { wackel: 14 } })
const PUNKTE = {
  einmal: true,
  // Die drei Punkte schwellen nacheinander an.
  draw(o, E) {
    for (let i = 0; i < 3; i++) {
      const t = Math.min(1, Math.max(0, o * 1.6 - i * 0.3))
      at(E['p' + i], { r: f(1.5 * (1 + 0.45 * Math.sin(Math.PI * t))) })
    }
  }
}
sym('more_vert', D(12, 5, 1.5, 'p0') + D(12, 12, 1.5, 'p1') + D(12, 19, 1.5, 'p2'), {}, PUNKTE)
sym('more_horiz', D(5, 12, 1.5, 'p0') + D(12, 12, 1.5, 'p1') + D(19, 12, 1.5, 'p2'), {}, PUNKTE)
sym('drag_indicator', G('a', D(9, 6, 1.4) + D(9, 12, 1.4) + D(9, 18, 1.4)) + G('b', D(15, 6, 1.4) + D(15, 12, 1.4) + D(15, 18, 1.4)), { a: { huepf: 1.2 }, b: { huepf: -1.2 } })
sym('menu', P('M4 7H20') + P('', 'l2') + P('', 'l3'), {}, {
  // Die unteren Zeilen ziehen sich gestaffelt zusammen.
  draw(o, E) {
    at(E.l2, { d: `M4 12H${f(20 - 5 * o)}` })
    at(E.l3, { d: `M4 17H${f(20 - 10 * o)}` })
  }
})

// ---------- Personen ----------
const SCHULTERN = 'M5 20V19A5 5 0 0 1 10 14H14A5 5 0 0 1 19 19V20'
sym('person', G('a', C(12, 8, 3.5)) + P(SCHULTERN), { a: { huepf: 1.3 } })
sym('person_off', (u) => G('a', durch(u, C(12.5, 7.5, 3.5) + P(SCHULTERN), 'M3 5L19 21')), { a: { pendelX: 1.5, k: 3 } })
sym('person_add', C(10, 8, 3.5) + P('M3 20V19A5 5 0 0 1 8 14H12A5 5 0 0 1 17 19V20') + G('a', P('M19 7V13M16 10H22')), { a: { dreh: 90, o: [19, 10] } })
sym('group', C(9, 8, 3.3) + P('M3 20V19A4.5 4.5 0 0 1 7.5 14.5H10.5A4.5 4.5 0 0 1 15 19V20') + G('a', P('M15.5 4.9A3.3 3.3 0 0 1 15.5 11.1') + P('M18 14.7A4.5 4.5 0 0 1 21 19V20')), { a: { x: 1.3 } })
sym('account_circle', C(12, 12, 9) + G('a', C(12, 10, 3)) + P('M6.5 18.6C7.6 16.7 9.6 15.7 12 15.7S16.4 16.7 17.5 18.6'), { a: { huepf: 1.1 } })
sym('smart_toy', P(rr(4, 8, 16, 12, 2.5)) + P('M2 13V16M22 13V16') + P('M9.5 16.8H14.5') + G('a', P('M12 8V4.8') + D(12, 4, 1.2)) + G('l', D(9, 12.8, 1.3)) + G('r', D(15, 12.8, 1.3)), { a: { wackel: 16, o: [12, 8] }, l: { pulsY: -0.85, o: [9, 12.8] }, r: { pulsY: -0.85, o: [15, 12.8] } })

// ---------- Sehen ----------
const AUGE = P('M2.5 12C4.5 7.5 8 5.5 12 5.5S19.5 7.5 21.5 12C19.5 16.5 16 18.5 12 18.5S4.5 16.5 2.5 12Z') + C(12, 12, 3)
sym('visibility', G('a', AUGE), { a: { pulsY: -0.75 } })
sym('visibility_off', (u) => G('a', durch(u, AUGE)), { a: { pulsY: -0.75 } })
sym('public', C(12, 12, 9) + P('M3 12H21') + P('', 'm'), {}, {
  // Der Längengrad wandert, als drehte sich die Kugel.
  draw(o, E) {
    const w = 3.5 + 2.3 * o, v = w * 0.72
    at(E.m, { d: `M12 3C${f(12 - v)} 5.5 ${f(12 - w)} 8.5 ${f(12 - w)} 12S${f(12 - v)} 18.5 12 21C${f(12 + v)} 18.5 ${f(12 + w)} 15.5 ${f(12 + w)} 12S${f(12 + v)} 5.5 12 3Z` })
  }
})

// ---------- Hinweise und Zustände ----------
const KREIS = K(C(12, 12, 9))
sym('info', KREIS + G('a', P('M12 11V16.5') + D(12, 7.7, 1.2)), { a: { huepf: 1.2 } })
sym('error', G('a', KREIS + P('M12 7.5V13') + D(12, 16.3, 1.2)), { a: { pendelX: 1.5, k: 3 } })
sym('report', G('a', K(P(rp([[8.3, 3], [15.7, 3], [21, 8.3], [21, 15.7], [15.7, 21], [8.3, 21], [3, 15.7], [3, 8.3]], 0.8))) + P('M12 7.5V13') + D(12, 16.3, 1.2)), { a: { pendelX: 1.5, k: 3 } })
sym('warning', G('a', K(P(rp([[12, 3.5], [21.5, 20], [2.5, 20]], 1.6))) + P('M12 10V14.2') + D(12, 17, 1.2)), { a: { pendelX: 1.5, k: 3 } })
sym('check_circle', KREIS + G('a', P('M8 12.3L11 15.3L16.2 9.5')), { a: { puls: 0.25 } })
sym('task_alt', P('M21 11.1V12A9 9 0 1 1 15.7 3.8') + G('a', P('M8.5 11.5L12 15L21 5.5')), { a: { puls: 0.18, o: [13, 11] } })
sym('remove_circle_outline', KREIS + G('a', P('M8 12H16')), { a: { puls: -0.3 } })
sym('block', C(12, 12, 9) + G('a', P('M5.6 5.6L18.4 18.4')), { a: { dreh: 180 } })
sym('check_box', K(P(rr(3.5, 3.5, 17, 17, 2.5))) + G('a', P('M7.5 12.3L10.7 15.5L16.7 8.8')), { a: { puls: 0.22 } })
sym('check_box_outline_blank', G('a', P(rr(3.5, 3.5, 17, 17, 2.5))), { a: { puls: 0.08 } })
sym('radio_button_checked', C(12, 12, 9) + G('a', D(12, 12, 4.4)), { a: { puls: 0.25 } })
sym('radio_button_unchecked', G('a', C(12, 12, 9)), { a: { puls: 0.07 } })
sym('auto_awesome', G('a', P('M10 5L11.8 10.2L17 12L11.8 13.8L10 19L8.2 13.8L3 12L8.2 10.2Z')) + G('b', P('M18.5 2.5L19.3 4.7L21.5 5.5L19.3 6.3L18.5 8.5L17.7 6.3L15.5 5.5L17.7 4.7Z')) + G('c', P('M18.5 15.5L19.3 17.7L21.5 18.5L19.3 19.3L18.5 21.5L17.7 19.3L15.5 18.5L17.7 17.7Z')), { a: { puls: 0.12, o: [10, 12] }, b: { puls: -0.55, o: [18.5, 5.5] }, c: { puls: -0.55, o: [18.5, 18.5] } })

// ---------- Geld ----------
sym('euro', G('a', P('M18 7A7 7 0 1 0 18 17') + P('M3.5 10.5H13M3.5 13.5H13')), { a: { huepf: 1.3 } })
sym('paid', C(12, 12, 9) + G('a', P('M14.8 9.4C14.3 8.3 13.3 7.8 12 7.8C10.3 7.8 9.2 8.7 9.2 9.9C9.2 11.2 10.3 11.7 12 12C13.7 12.3 14.8 12.9 14.8 14.2C14.8 15.4 13.7 16.2 12 16.2C10.6 16.2 9.6 15.7 9.1 14.6M12 6V7.8M12 16.2V18')), { a: { kipp: 1 } })
sym('payments', P('M22 9V17.5A2 2 0 0 1 20 19.5H6') + G('a', P(rr(2.5, 5.5, 16.5, 10, 1.5)) + C(10.75, 10.5, 2.3)), { a: { x: -0.6, y: -1 } })
sym('calculate', P(rr(3.5, 3.5, 17, 17, 2.5)) + G('a', P('M7 8.5H11M9 6.5V10.5')) + P('M13.5 8.5H17') + P('M7.3 14L10.7 17.4M10.7 14L7.3 17.4') + P('M13.5 14.5H17M13.5 17H17'), { a: { dreh: 90, o: [9, 8.5] } })

// ---------- Ansichten und Aufbau ----------
sym('view_column', P(rr(3, 5, 18, 14, 2)) + P('', 'a') + P('', 'b'), {}, {
  // Die mittlere Spalte wird breiter.
  draw(o, E) {
    at(E.a, { d: `M${f(9 - 1.5 * o)} 5V19` })
    at(E.b, { d: `M${f(15 + 1.5 * o)} 5V19` })
  }
})
sym('view_agenda', P('', 'a') + P('', 'b'), {}, {
  // Die beiden Karten tauschen ihre Höhe.
  draw(o, E) {
    const h = 7 - 2.5 * o
    at(E.a, { d: rr(4, 4, 16, h, 1.5) })
    at(E.b, { d: rr(4, 6 + h, 16, 14 - h, 1.5) })
  }
})
sym('table_rows', P(rr(3, 4, 18, 16, 2)) + P('', 'a') + P('', 'b'), {}, {
  // Die mittlere Zeile wird höher.
  draw(o, E) {
    at(E.a, { d: `M3 ${f(9.5 - 1.3 * o)}H21` })
    at(E.b, { d: `M3 ${f(14.5 + 1.3 * o)}H21` })
  }
})
sym('category', G('a', P(rp([[12, 3], [16.5, 10], [7.5, 10]], 0.8))) + G('b', C(17, 17, 3.5)) + G('c', P(rr(4, 13.5, 6.5, 6.5, 1))), { a: { y: -0.9 }, b: { x: 0.9, y: 0.9 }, c: { x: -0.9, y: 0.9 } })
sym('account_tree', P(rr(3, 9, 6, 6, 1)) + P('M9 12H12M12 6.5V17.5M12 6.5H15M12 17.5H15') + G('a', P(rr(15, 3.5, 6, 6, 1))) + G('b', P(rr(15, 14.5, 6, 6, 1))), { a: { puls: 0.18, o: [18, 6.5] }, b: { puls: 0.18, o: [18, 17.5] } })
sym('add_business', P('M4 11V21H14V11') + P('M3 11L5 5H13L15 11Z') + P('M7.5 21V16H10.5V21') + G('a', P('M19 13V19M16 16H22')), { a: { dreh: 90, o: [19, 16] } })
sym('home', G('a', P('M4 10.5L12 4L20 10.5V19A1.5 1.5 0 0 1 18.5 20.5H5.5A1.5 1.5 0 0 1 4 19Z') + P('M10 20.5V15H14V20.5')), { a: { huepf: 1.3 } })
