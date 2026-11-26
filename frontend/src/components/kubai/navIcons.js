// Copyright (c) 2026 Kubai087. Licensed under the Kubai Icons License; see LICENSE.
// Icons der Hauptnavigation: einfarbige Linien-Icons im Stil der Material Symbols (24er-Raster,
// Strich 1.8, currentColor), bewegt mit der Kubai-Engine. Jedes Icon: svg(u) baut die Teile,
// target(hover) liefert die Zahlen für Ruhe und Hover, draw(p, E) schreibt die interpolierten
// Zahlen zurück. Zwei Zustände, 460 ms, die Bewegung ist das, was das Ding selbst tun würde.
import { rp, rr, at, f } from './engine.js'

const VIEWBOX = '0 0 24 24'
const LINIE = 'fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"'

// Zahnrad um (12, 12): `zaehne` Zähne zwischen Innenradius ri und Außenradius ra.
function zahnrad(zaehne, ra, ri) {
  const pts = [], step = 360 / zaehne, rad = (d) => (d * Math.PI) / 180;
  for (let i = 0; i < zaehne; i++) {
    const a = i * step;
    [[ri, a - step * 0.3], [ra, a - step * 0.17], [ra, a + step * 0.17], [ri, a + step * 0.3]].forEach(([r, d]) => {
      pts.push([12 + r * Math.sin(rad(d)), 12 - r * Math.cos(rad(d))]);
    });
  }
  return rp(pts, 0.8);
}
const ZAHNRAD = zahnrad(8, 10, 7.6);

// Markise von x 3 bis 21: oben gerade, unten vier Bögen, die bis yb + 2.25 hängen.
const markise = (yb) => {
  let d = `M3 3.5H21V${f(yb)}`;
  for (let i = 1; i <= 4; i++) d += `A2.25 2.25 0 0 1 ${f(21 - 4.5 * i)} ${f(yb)}`;
  return d + 'Z';
};

// Bon mit vier abgerissenen Zacken unten; oben offen, der Druckerschlitz verdeckt den Anfang.
const BON = (() => {
  let d = 'M6.5 4V17.5';
  const w = 11 / 8;
  for (let i = 1; i <= 8; i++) d += `L${f(6.5 + w * i)} ${i % 2 ? 19 : 17.5}`;
  return d + 'V4';
})();

// Anhänger um den Ursprung, Spitze links, mit Loch.
const ANHAENGER = rp([[-8.5, 0], [-3.8, -5], [8.5, -5], [8.5, 5], [-3.8, 5]], [1.4, 1.4, 1.8, 1.8, 1.4]);

export const NAV_ICONS = [
  {
    name: 'Dashboard',
    viewBox: VIEWBOX,
    // Rahmen mit drei Feldern; beim Hover verschieben sich die Trennlinien, die Felder tauschen ihre Größe.
    svg: () => `<g ${LINIE}><path d="${rr(3, 3, 18, 18, 2.5)}"/><path data-n="v"/><path data-n="h"/></g>`,
    target: (h) => ({ x: h ? 13.5 : 10.5, y: h ? 14.5 : 11 }),
    draw(p, E) {
      at(E.v, { d: `M${f(p.x)} 3V21` });
      at(E.h, { d: `M${f(p.x)} ${f(p.y)}H21` });
    }
  },
  {
    name: 'Belege',
    // Ein Bon kommt aus dem Druckerschlitz; beim Hover druckt er ein Stück weiter.
    svg: (u) => `<clipPath id="${u.id('schlitz')}"><rect x="0" y="7" width="24" height="17"/></clipPath>
      <g ${LINIE}>
        <g clip-path="url(#${u.id('schlitz')})"><g data-n="bon"><path d="${BON}"/><path d="M9.5 10.5H14.5M9.5 14H12.5"/></g></g>
        <path d="${rr(3, 3, 18, 4.5, 2.25)}"/>
      </g>`,
    viewBox: VIEWBOX,
    target: (h) => ({ y: h ? 2.2 : 0 }),
    draw(p, E) { at(E.bon, { transform: `translate(0 ${f(p.y)})` }); }
  },
  {
    name: 'Hochladen',
    viewBox: VIEWBOX,
    // Blatt mit Eselsohr; der Pfeil darauf hebt sich beim Hover.
    svg: () => `<g ${LINIE}>
        <path d="M6 2H14L20 8V20A2 2 0 0 1 18 22H6A2 2 0 0 1 4 20V4A2 2 0 0 1 6 2Z"/><path d="M14 2V7A1 1 0 0 0 15 8H20"/>
        <path data-n="pfeil"/></g>`,
    target: (h) => ({ t: h ? 9.5 : 11.5 }),
    draw(p, E) {
      const t = p.t;
      at(E.pfeil, { d: `M12 ${f(t + 7)}V${f(t)}M9 ${f(t + 3)}L12 ${f(t)}L15 ${f(t + 3)}` });
    }
  },
  {
    name: 'Korrespondenten',
    viewBox: VIEWBOX,
    // Ladengeschäft mit Bogenmarkise; beim Hover rollt die Markise weiter herunter.
    svg: () => `<g ${LINIE}><path data-n="haus"/><path d="M10 21V16.5H14V21"/><path data-n="markise"/></g>`,
    target: (h) => ({ yb: h ? 8.6 : 6.6 }),
    draw(p, E) {
      at(E.markise, { d: markise(p.yb) });
      const top = p.yb + 2.2;
      at(E.haus, { d: `M5 ${f(top)}V20A1 1 0 0 0 6 21H18A1 1 0 0 0 19 20V${f(top)}` });
    }
  },
  {
    name: 'Artikel',
    viewBox: VIEWBOX,
    // Archivkarton; beim Hover hebt sich der Deckel und kippt leicht.
    svg: () => `<g ${LINIE}>
        <path d="M4 8V19A2 2 0 0 0 6 21H18A2 2 0 0 0 20 19V8"/><path d="M10 12H14"/>
        <path data-n="deckel" d="${rr(2.5, 3, 19, 5, 1.5)}"/></g>`,
    target: (h) => ({ d: h ? [0.4, -2.2, -7] : [0, 0, 0] }),
    draw(p, E) {
      at(E.deckel, { transform: `translate(${f(p.d[0])} ${f(p.d[1])}) rotate(${f(p.d[2])} 12 5.5)` });
    }
  },
  {
    name: 'Tags',
    viewBox: VIEWBOX,
    // Preisanhänger; beim Hover schwingt er um sein Loch.
    svg: () => `<g data-n="tag"><path d="${ANHAENGER}" ${LINIE}/><circle cx="-3.4" cy="0" r="1.5" fill="currentColor"/></g>`,
    target: (h) => ({ a: h ? 22 : 45 }),
    draw(p, E) {
      // Drehpunkt ist das Loch bei (9.6, 9.6); in Ruhe (45°) liegt die Mitte des Anhängers bei (12, 12).
      at(E.tag, { transform: `translate(9.6 9.6) rotate(${f(p.a)}) translate(3.4 0)` });
    }
  },
  {
    name: 'Export',
    viewBox: VIEWBOX,
    // Pfeil über einer Ablage; beim Hover sinkt der Pfeil hinein.
    svg: () => `<g ${LINIE}><path d="M4 15V18A2 2 0 0 0 6 20H18A2 2 0 0 0 20 18V15"/><path data-n="pfeil"/></g>`,
    target: (h) => ({ t: h ? 15 : 13 }),
    draw(p, E) {
      const t = p.t;
      at(E.pfeil, { d: `M12 ${f(t - 10)}V${f(t)}M7.5 ${f(t - 4.5)}L12 ${f(t)}L16.5 ${f(t - 4.5)}` });
    }
  },
  {
    name: 'Einstellungen',
    viewBox: VIEWBOX,
    // Zahnrad; beim Hover dreht es sich um einen Zahn weiter.
    svg: () => `<g ${LINIE}><path data-n="rad" d="${ZAHNRAD}"/><circle cx="12" cy="12" r="3.2"/></g>`,
    target: (h) => ({ rot: h ? 45 : 0 }),
    draw(p, E) { at(E.rad, { transform: `rotate(${f(p.rot)} 12 12)` }); }
  }
];

// Linien-Icons brauchen weder Verläufe noch Filter.
NAV_ICONS.forEach((i) => { i.mono = true; });
