# Biber-Rechnungen – Entscheidungsprotokoll (Grilling)

Stand: 2026-11-23 · Runde 1 abgeschlossen

## Bestätigte Annahmen
- A1 Positionsdaten (Artikel, Menge, Einzelpreis) werden extrahiert.
- A2 Mail, Ordner und Upload laufen in eine gemeinsame Pipeline: Eingang → Formaterkennung → Extraktion → Prüfung → Ablage.
- A3 Originale bleiben unverändert; extrahierte Daten liegen getrennt in der DB und sind korrigierbar.
- A4 Eine responsive React-App für Desktop und Mobile, PWA darauf aufgesetzt.
- A5 Der MCP-Server nutzt dieselbe Domänenlogik und dieselben Rechte wie die REST-API.

## Runde 1

| # | Thema | Entscheidung |
|---|---|---|
| Q1 | Zweck | Zuerst Eingangsrechnungen; das Schreiben eigener Rechnungen kommt später |
| Q2 | Nutzerkreis | Wenige Benutzer, ein gemeinsamer Datenbestand (kein Multi-Mandant) |
| Q3 | Steuerlich | **Voll geschäftlich: GoBD inklusive Verfahrensdokumentation** |
| Q4 | Menge | 100–1.000 Belege pro Monat |
| Q5 | Hosting | Self-hosted mit Docker Compose; Azure nur für Document Intelligence |
| Q6 | Extraktion | Azure Document Intelligence, EU-Region (prebuilt-invoice / prebuilt-receipt) |
| Q7 | Mail | IMAP-Polling eines eigenen Postfachs; verarbeitete Mails in einen IMAP-Ordner |
| Q8 | Mailinhalt | v1: nur Anhänge (PDF, XML, Bilder) |
| Q9 | Eingangsordner | Gemountetes Verzeichnis / Docker-Volume |
| Q10 | Ablage | Primärer Speicher aller Originale: Ablage/{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnr}.pdf |
| Q11 | Dubletten | Hash + fachlicher Abgleich (Korrespondent + Rechnungsnr. + Betrag), markieren statt doppelt zählen |
| Q12 | Belegarten | Rechnungen, Gutschriften/Stornos, Kassenbons (Foto), sonstige Quittungen; **keine** Verträge/Abos |
| Q13 | E-Rechnung | In-Process in der C#-API (ZUGFeRD-csharp + UBL-Parser), optional KoSIT-Validierung |
| Q14 | Vorrang | Eingebettetes/reines XML ist maßgeblich; dann kein Document Intelligence |
| Q15 | OCR | **Kein OCR-Fallback**; das entfällt |
| Q16 | Prüfung | **Plausibilitätsprüfungen** (z. B. Gesamtsumme). Schlägt eine fehl, bekommt der Beleg den Tag „zu prüfen“ |
| Q17 | Artikel | Normalisierter Positionstext pro Korrespondent; manuelles Zusammenführen zu Stammartikeln über Händler hinweg |
| Q18 | Preisvergleich | Einzelpreis, plus Grundpreis (€/kg, €/l), wenn erkennbar |
| Q19 | Korrespondenten | Automatisch über USt-IdNr., IBAN und Namen; manuelles Zusammenführen |
| Q20 | Logo | Ausschnitt aus Seite 1; manueller Upload überschreibt; **Favicon als Fallback** |
| Q21 | Kategorien | Kategorie am Korrespondenten als Default, pro Rechnung überschreibbar, plus freie Tags |
| Q22 | Beträge | Netto, USt und Brutto für Kopf und Positionen; Währung gespeichert, Auswertung nur EUR |
| Q23 | Login | Benutzer + Passwort, TOTP-2FA, Google |
| Q24 | MCP-Umfang | Lesen + Upload |
| Q25 | MCP-Transport | Streamable HTTP als Teil der API |
| Q26 | DB | PostgreSQL, Volltext (german) + pg_trgm |
| Q27 | Backend | .NET 10, Minimal APIs, EF Core (Npgsql), Hangfire (Postgres) |
| Q28 | Frontend | Vite, React, TypeScript, TanStack Query/Router, shadcn/ui + Tailwind, Recharts, PDF.js, Material Icons |
| Q29 | Mobile | PWA (installierbar, Kamera-Upload, Share-Target unter Android) |
| Q30 | Repo | Monorepo (/backend, /frontend, /docs, docker-compose.yml); Remote: https://github.com/BenjaminBiber/Biber-Rechnungen.git |

## Runde 2

Bestätigte Annahmen:
- B1 Volltext kommt aus Document Intelligence bzw. aus dem XML.
- B2 Bei E-Rechnungen ist das XML das Original; Anzeige über KoSIT-XSLT.
- B3 „zu prüfen“ ist ein System-Tag, der nur durch Bestätigen verschwindet.
- B4 Logo-Reihenfolge: Upload > PDF-Ausschnitt > Favicon.
- B5 ASP.NET Core Identity + Google.
- B6 Eine Instanz plus Hangfire reicht.
- B7 Lückenloses Änderungsprotokoll.

| # | Thema | Entscheidung |
|---|---|---|
| R2-1 | Organisation | **Kleine Firma, 1–2 Benutzer, nur geschäftliche Belege.** Damit ist der Widerspruch zwischen Q2 und Q3 aufgelöst |
| R2-2 | Buchhaltung heute | Selbst (EÜR/Excel/Elster); Rechnungen werden derzeit ausgedruckt und in Ordnern abgeheftet |
| R2-3 | Rolle der App | Belegsammler, Archiv und Auswertung; liefert Daten an die Buchhaltung |
| R2-4 | Ausgangsrechnungen | Nur im Datenmodell vorbereitet (Richtung Eingang/Ausgang, Korrespondent als Lieferant/Kunde) |
| R2-5 | Unveränderbarkeit | Nur die App schreibt, Benutzer lesen; SHA-256 + nächtliche Integritätsprüfung; **Ablage-Share ist WORM in Samba** |
| R2-6 | Ablagezeitpunkt | Interner Eingang, nach der Extraktion einmalig in die Ablage; Pfad danach fix |
| R2-7 | Festschreibung | **Keine Festschreibung**, nur das Änderungsprotokoll |
| R2-8 | Aufbewahrung | Löschsperre bis Fristende, danach nur ein Vorschlag, nie automatisch löschen |
| R2-9 | Backup | **Das vorhandene NAS-Backup reicht** |
| R2-10 | Verfahrensdoku | Markdown in /docs/verfahrensdokumentation, PDF-Export; Entwurf von Claude |
| R2-11 | Plausibilität | Alle 9 Prüfungen: Positionssumme, Netto + USt = Brutto, Pflichtfelder, Datum, §14 UStG, Dublette, neuer Korrespondent, DI-Konfidenz, KoSIT-Validierung |
| R2-12 | DI-Fehler | Retry mit Backoff, danach Status „Fehler“ + „zu prüfen“; manuelle Erfassung und erneuter Anstoß möglich |
| R2-13 | DI-Kosten | Seitenlimit pro Monat, Warnung bei 80 %, Pause bei 100 % |
| R2-14 | Kassenbons | **Kein Kassenbon-Scan per Kamera** |
| R2-15 | Postfach | Klassischer Provider, IMAP mit App-Passwort |
| R2-16 | Registrierung | **Offene Registrierung mit Freigabe durch den Admin** |
| R2-17 | Rollen | Admin, Benutzer, Nur-Lesen (ohne Festschreibung, siehe R2-7) |
| R2-18 | MCP-Clients | Zuerst Claude Code/Desktop mit API-Token; OAuth (OpenIddict) später |
| R2-19 | Offline | App-Shell offline, Listen gecacht, Upload-Warteschlange |
| R2-20 | Erreichbarkeit | **Nicht von außen erreichbar** |

## Offene Spannungen aus Runde 2 → Runde 3
- Q12 nennt „Kassenbons (Foto)“, R2-14 sagt „kein Kassenbon-Scan“. Bleiben Kassenbons als Datei erlaubt?
- R2-20 „nur im LAN“: PWA, Service Worker und Google-Login brauchen HTTPS und eine echte Domain.
- R2-9 „das NAS-Backup reicht“: Die Postgres-Datenbank (extrahierte Daten, Änderungsprotokoll) muss dafür auf dem NAS-Backup landen.
- R2-2: Bei EÜR zählt das Zahlungsdatum (Abflussprinzip). Daraus folgen Zahlungsstatus und Export.
- Papierrechnungen per Post und der Papier-Altbestand.

## Runde 3

Bestätigte Annahmen: C1–C8 (nur geschäftlich; WORM-Ablage; keine Festschreibung; kein Kamera-Scan; nur LAN; Registrierung mit Freigabe, der erste Benutzer wird Admin; alle Prüfungen konfigurierbar; Belegliste mit Filtern und Volltext).

| # | Thema | Entscheidung |
|---|---|---|
| R3-1 | USt-Status | **Unbekannt.** Wird eine Einstellung in der App |
| R3-2 | Docker | Läuft direkt auf dem NAS (System noch offen) |
| R3-3 | Domain | IONOS; heute schon Let's Encrypt über einen nginx-Reverse-Proxy |
| R3-4 | HTTPS | Interner Hostname unter eigener Domain, DNS-01, kein Port nach außen |
| R3-5 | DB-Backup | Backup-Container mit nächtlichem pg_dump in einen NAS-Ordner, rotierend 30 Tage |
| R3-6 | Benachrichtigungen | **Nur in der App** (Glocke/Zähler), keine Mails |
| R3-7 | Papierpost | Scan-to-SMB in den Eingangsordner; Papier wird zusätzlich aufbewahrt |
| R3-8 | Scanstapel | Eine Datei ist ein Beleg; manuelles Aufteilen in der UI, Original bleibt |
| R3-9 | Altbestand | Stichtag; Altbestand bleibt in Ordnern |
| R3-10 | Kassenbons | **Werden gar nicht erfasst** |
| R3-11 | Zahlungsstatus | **Als Freitext-Tag am Beleg; dazu ein automatischer „Inbox“-Tag wie bei paperless-ngx** |
| R3-12 | Kategorien | **Frei, ohne Vorbelegung** |
| R3-13 | Export | Excel/CSV-Belegliste + ZIP der Originale, Summen, Vorsteuer bei Regelbesteuerung |
| R3-14 | Zeitbasis | Rechnungsdatum, umschaltbar auf Zahlungsdatum |
| R3-15 | Dashboard | Alle 8 Kacheln, inkl. DI-Verbrauch |
| R3-16 | Ausbaustufen | Phasen 1–4 wie vorgeschlagen |
| R3-17 | Planung | Nur /docs/plan.md, keine Tickets |

## Offene Spannungen aus Runde 3 → Runde 4
- R3-11 (Zahlung nur als Tag) passt nicht zu R3-13, R3-14 und R3-15: Export, EÜR-Umschaltung und „offene Rechnungen“ brauchen ein Zahlungsdatum.
- R3-12 (freie Kategorien) passt nicht zu R3-13 („Summen je EÜR-Zeile“).
- R3-2/R3-3: Welches NAS-System (bestimmt, wie WORM umgesetzt wird) und wie der vorhandene nginx läuft.
- R3-11 Inbox wie paperless: Wie verhält er sich zu „zu prüfen“, und gibt es automatische Zuordnungsregeln?

## Runde 4 (Abschluss)

Bestätigte Annahmen:
- D1 USt-Status ist eine Einstellung.
- D2 Kategorien haben ein optionales Feld „EÜR-Zeile“.
- D3 Nur prebuilt-invoice.
- D4 Fälligkeitsdatum aus der Rechnung.
- D5 Benachrichtigungen nur in der App.
- D6 Phasen angepasst.

| # | Thema | Entscheidung |
|---|---|---|
| R4-1 | „NAS“ | **Samba-Container auf demselben Docker-Host wie die App** |
| R4-2 | Proxy | Nginx Proxy Manager auf demselben Host |
| R4-3 | Erreichbarkeit | Der Proxy ist nur im LAN erreichbar |
| R4-4 | Reverse Proxy | Vorhandenen NPM nutzen, LAN-Access-List, DNS-01 über IONOS; kein Caddy |
| R4-5 | Zahlung | **Nur Tags, ohne Datum.** Damit entfallen die Umschaltung auf das Zahlungsdatum und die Export-Spalte; „überfällig“ = kein Zahlungs-Tag und Fälligkeit überschritten |
| R4-6 | Inbox | Inbox-Tag und System-Tag „zu prüfen“ getrennt |
| R4-7 | Zuordnung | Regeln + lernendes Auto-Matching (ML.NET) |
| R4-8 | Weiteres Vorgehen | Plan + Verfahrensdoku zur Prüfung; erst nach OK: git init, Remote, Push, Phase 1 |

**Frontier leer.** Ergebnis: [`docs/plan.md`](../docs/plan.md) und [`docs/verfahrensdokumentation/`](../docs/verfahrensdokumentation/verfahrensdokumentation.md). Offene Prüfpunkte P1–P6 stehen im Plan, Abschnitt 13.
