# UI-Design (Quelltext)

Kopie des Design-Canvas „Biber-Rechnungen – UI-Design“
(<https://claude.ai/artifact/MshU1GCVcMfsi5g5NN45dY>), Stand 2026-11-25.
Das Design ist maßgeblich für alle Views. Die Dateien sind **Vorlage, kein Produktivcode**:
Sie werden gelesen, nicht eingebunden.

## Dateien lesen

Jede `*.dc.html` ist ein Artboard. Der Aufbau steht im `<x-dc>`-Block als HTML mit Inline-Styles
(Abstände, Farben, Schriftgrößen stehen dort exakt). `{{ name }}` sind Platzhalter, `<sc-for>` ist
eine Schleife, `<sc-if>` eine Bedingung, `<dc-import name="X">` bindet `X.dc.html` ein.
Die Werte der Platzhalter und die **Beispieldaten** stehen unten in `renderVals()`.
`canvas.json` enthält Titel, Größe und Seite je Artboard.

| Datei | Inhalt |
|---|---|
| `Login.dc.html` | Anmeldung in fünf Schritten: anmelden, totp, registrieren, freigabe, ersteinrichtung |
| `Main.dc.html` | Dashboard |
| `Belege.dc.html` | Belegliste in drei Darstellungen (Tabelle, kleine Karten, große Karten) |
| `BelegeKleineKarten.dc.html`, `BelegeGrosseKarten.dc.html` | dieselbe Seite mit anderer Startdarstellung |
| `BelegDetail.dc.html` | Beleg-Detail mit Viewer und sechs Tabs |
| `Upload.dc.html` | Hochladen und Import-Vorgänge |
| `Korrespondenten.dc.html`, `KorrespondentDetail.dc.html` | Korrespondenten |
| `Artikel.dc.html`, `ArtikelDetail.dc.html` | Artikel und Stammartikel |
| `TagsKategorien.dc.html` | Tags und Kategorien mit Bearbeiten-Dialog |
| `Export.dc.html` | Export |
| `Einstellungen.dc.html` | Einstellungen mit zwölf Bereichen |
| `Menues.dc.html` | alle Menüs und Popover (Befehlspalette, Benutzermenü, Glocke, Zeilenmenü, Auswahl-Popover, Filter) |
| `Dialoge.dc.html` | alle Dialoge und Toasts |
| `MobilDashboard.dc.html`, `MobilBelege.dc.html`, `MobilBelegDetail.dc.html`, `MobilUpload.dc.html` | Mobil (PWA), 390 px |
| `Sidebar.dc.html`, `SidebarEingeklappt.dc.html`, `Topbar.dc.html`, `MobilNav.dc.html` | Bausteine: Seitenleiste (ausgeklappt 240 px / eingeklappt 72 px), Kopfleiste, Bottom-Navigation |
| `biber-rechnungen-logo.svg` | Biber-Logo (Quelle für `frontend/src/components/Logo.tsx`) |

## Look „Paperless-Indigo“

- Akzent: Indigo `#2F2D86` (Hover `#24226B`), Arbeitsfläche `#F4F5F7`, Karten weiß mit Rand `#E3E6EB`, Radius 12 px.
- Seitenleiste und Marke: Schiefer-Indigo `#34334F` (Hover `#45445F`, aktiver Eintrag `#4A4968`, Gruppentitel `#ABAAC2`, Linie/Badge `#5C5B78`); einklappbar auf 72 px nur mit Icons.
- Logo: Biber-Umriss in Weiß auf der Seitenleiste bzw. in `#34334F` im mobilen Kopf. Anmeldung: Bürofoto (`frontend/src/assets/anmeldung-buero.webp`, im Design ein Upload `/_blob/…`) mit Logo-Fahne links, Formular rechts in 560 px.
- Text `#151922`, gedämpft `#5E6676` / `#4A5262`, Linien `#EEF0F3`, Eingabefeld-Rand `#D5D9E0`.
- Statusfarben, immer mit Text, nie nur Farbe:
  - Orange = zu prüfen: Fläche `#FDF0E3`, Text `#A44A06`
  - Grün = ok/bezahlt: `#E5F4EC` / `#17703F`
  - Rot = überfällig/Fehler: `#FDECEA` / `#B42318`
  - Blau = Inbox/Info: `#E7E7F4` / `#2F2D86`
  - Grau `#EEF0F3` / `#4A5262`, Petrol `#E1F3F4` / `#0E6168`, Lila `#F0EAFB` / `#5B2EA6`
- Schrift: IBM Plex Sans (400/500/600/700), Beträge mit Tabellenziffern; IBM Plex Mono für Rechnungsnummern, Pfade, Hashes.
- Icons: Google Material Symbols Outlined (Ligaturnamen wie im Design, z. B. `receipt_long`).
- Chips: Höhe 22 px, Radius 11 px, 12 px/500. Buttons: Höhe 36 px (klein 30 px), Radius 8 px.
- Toasts unten rechts, verschwinden nach 6 s; Fehler bleiben stehen.
