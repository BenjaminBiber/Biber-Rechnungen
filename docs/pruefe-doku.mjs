// Prüft die Betriebs- und Verfahrensdokumentation auf Vollständigkeit (ohne Abhängigkeiten).
// Aufruf aus dem Repo-Hauptordner: node docs/pruefe-doku.mjs
import { existsSync, readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const wurzel = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const lies = (pfad) => readFileSync(join(wurzel, pfad), "utf8");

const BETRIEB = "docs/betrieb.md";
const VERFAHREN = "docs/verfahrensdokumentation/verfahrensdokumentation.md";
const README = "README.md";
const SAMBA = "docker/samba/smb.conf.example";

const MCP_TOOLS = [
  "belege_suchen",
  "beleg_abrufen",
  "kosten_auswerten",
  "korrespondent_abrufen",
  "artikel_suchen",
  "artikel_preisverlauf",
  "beleg_hochladen",
];

const PRUEFUNGEN = [
  "Positionssumme",
  "Netto + Steuer = Brutto",
  "Pflichtfelder",
  "Datum",
  "§ 14 UStG",
  "Dublettenverdacht",
  "Neuer Korrespondent",
  "Erkennungssicherheit",
  "KoSIT-Validierung",
  "Bankverbindung bekannt",
];

function envVariablen() {
  return lies(".env.example")
    .split(/\r?\n/)
    .map((zeile) => zeile.match(/^([A-Z][A-Z0-9_]*)=/))
    .filter(Boolean)
    .map((treffer) => treffer[1]);
}

function composeDienste() {
  const zeilen = lies("docker-compose.yml").split(/\r?\n/);
  const dienste = [];
  let imAbschnitt = false;
  for (const zeile of zeilen) {
    if (/^services:\s*$/.test(zeile)) {
      imAbschnitt = true;
      continue;
    }
    if (imAbschnitt && /^\S/.test(zeile)) break;
    const treffer = imAbschnitt && zeile.match(/^ {2}([A-Za-z0-9_.-]+):\s*$/);
    if (treffer) dienste.push(treffer[1]);
  }
  return dienste;
}

function relativeLinks(pfad) {
  const text = lies(pfad).replace(/```[\s\S]*?```/g, "");
  const links = [...text.matchAll(/\]\(([^)\s]+)\)/g)].map((t) => t[1]);
  return links
    .filter((ziel) => !/^[a-z][a-z0-9+.-]*:/i.test(ziel) && !ziel.startsWith("#"))
    .map((ziel) => decodeURIComponent(ziel.split("#")[0]))
    .filter((ziel) => !existsSync(resolve(join(wurzel, dirname(pfad)), ziel)))
    .map((ziel) => `${pfad}: Link auf fehlende Datei ${ziel}`);
}

const pruefungen = [
  [
    "Jede Variable aus .env.example steht in docs/betrieb.md",
    () => {
      const betrieb = lies(BETRIEB);
      return envVariablen()
        .filter((v) => !betrieb.includes(v))
        .map((v) => `${BETRIEB}: Variable ${v} fehlt`);
    },
  ],
  [
    "Jeder Dienst aus docker-compose.yml steht in Betriebs- und Verfahrensdokumentation",
    () => {
      const dienste = composeDienste();
      if (dienste.length === 0) return ["docker-compose.yml: keine Dienste gefunden"];
      return [BETRIEB, VERFAHREN].flatMap((pfad) => {
        const text = lies(pfad);
        return dienste.filter((d) => !text.includes(`\`${d}\``)).map((d) => `${pfad}: Dienst ${d} fehlt`);
      });
    },
  ],
  [
    "Alle sieben MCP-Tools und alle zehn Prüfungen stehen in der Verfahrensdokumentation",
    () => {
      const text = lies(VERFAHREN);
      return [
        ...MCP_TOOLS.filter((t) => !text.includes(t)).map((t) => `${VERFAHREN}: MCP-Tool ${t} fehlt`),
        ...PRUEFUNGEN.filter((p) => !text.includes(p)).map((p) => `${VERFAHREN}: Prüfung „${p}“ fehlt`),
      ];
    },
  ],
  ["Alle relativen Links zeigen auf vorhandene Dateien", () => [README, BETRIEB, VERFAHREN].flatMap(relativeLinks)],
  [
    "Die Verfahrensdokumentation hat die Kopfzeilen Version: und Stand:",
    () => {
      const text = lies(VERFAHREN);
      return ["Version:", "Stand:"]
        .filter((k) => !new RegExp(`^${k}\\s*\\S`, "m").test(text))
        .map((k) => `${VERFAHREN}: Kopfzeile ${k} fehlt`);
    },
  ],
  [
    "smb.conf.example enthält beide Freigaben und vfs objects = worm",
    () => {
      const text = lies(SAMBA);
      return [
        [/^\s*\[Rechnungen-Eingang\]\s*$/m, "Freigabe [Rechnungen-Eingang]"],
        [/^\s*\[Rechnungen-Ablage\]\s*$/m, "Freigabe [Rechnungen-Ablage]"],
        [/^\s*vfs objects\s*=\s*worm\s*$/m, "vfs objects = worm"],
      ]
        .filter(([muster]) => !muster.test(text))
        .map(([, name]) => `${SAMBA}: ${name} fehlt`);
    },
  ],
];

let fehlgeschlagen = 0;
for (const [name, pruefe] of pruefungen) {
  let luecken;
  try {
    luecken = pruefe();
  } catch (fehler) {
    luecken = [String(fehler.message ?? fehler)];
  }
  if (luecken.length === 0) {
    console.log(`OK    ${name}`);
  } else {
    fehlgeschlagen++;
    console.log(`FEHLT ${name}`);
    for (const luecke of luecken) console.log(`      - ${luecke}`);
  }
}

console.log(`\n${pruefungen.length - fehlgeschlagen} von ${pruefungen.length} Prüfungen bestanden.`);
process.exit(fehlgeschlagen === 0 ? 0 : 1);
