# Biber-Rechnungen – Umsetzungsplan

Stand: 2026-11-23 · Ergebnis aus 4 Grilling-Runden (Protokoll: [`planung/entscheidungen.md`](../planung/entscheidungen.md))
Status: **Entwurf zur Prüfung.** Code, Git-Repo und Push erst nach deinem OK (R4-8).

---

## 1. Ziel und Rahmen

Ein selbst gehostetes Belegarchiv für eine kleine Firma (1–2 Benutzer, nur geschäftliche Belege). Es sammelt Eingangsrechnungen aus E-Mail, Scanner bzw. Ordner und Upload, liest sie aus, prüft sie auf Plausibilität, legt sie GoBD-konform ab und wertet Kosten, Korrespondenten und Artikel aus. Gebucht wird weiterhin selbst (EÜR/Excel/Elster); die App liefert Belege und Exporte dafür.

**Nicht im Umfang:** Kassenbons, Kamera-Scan, OCR, Zahlungsdatum, Festschreibung, Mails an Benutzer, Zugriff von außen, Mandanten, Buchen/Kontieren. Ausgangsrechnungen kommen später; das Datenmodell ist dafür vorbereitet.

| Leitplanke | Konsequenz |
|---|---|
| GoBD (Q3) | Originale unveränderbar (WORM + SHA-256), lückenloses Änderungsprotokoll, Löschsperre 8 Jahre, Verfahrensdokumentation |
| Nur LAN (R2-20) | HTTPS über den vorhandenen Nginx Proxy Manager mit LAN-Hostname; kein Port nach außen |
| 100–1.000 Belege pro Monat (Q4) | Eine API-Instanz, Hangfire in-process, Postgres reicht |

## 2. Systemüberblick

```
                    LAN
 Scanner ──SMB──► [Samba-Container] ── Share "Eingang" (rw) ─┐
 Benutzer ◄─SMB── [Samba-Container] ── Share "Ablage" (ro, vfs_worm) ◄─┐
                                                              │       │
 Browser/PWA ──HTTPS──► [Nginx Proxy Manager] ──► [api]  ◄────┘ bind  │ bind mount
 Claude Code/Desktop ──HTTPS /mcp──────────────────►  │   mounts       │ (schreibt 1×)
                                                      ├──► [postgres]  │
 IMAP-Postfach ◄──────── IMAP (ausgehend) ────────────┤──► [kosit]  (Java-Sidecar)
 Azure Document Intelligence (EU) ◄── HTTPS ──────────┤
 Favicons (Websites) ◄─── HTTPS ──────────────────────┘
                                         [db-backup] ── pg_dump ──► Backup-Ordner (vom vorhandenen Backup mitgesichert)
```

Alles läuft auf dem vorhandenen Docker-Host, auf dem auch der Samba-Container und Nginx Proxy Manager laufen.

### docker-compose-Dienste
| Dienst | Image/Inhalt | Zweck |
|---|---|---|
| `api` | .NET 10, liefert zusätzlich das gebaute React-Frontend aus | REST-API, MCP-Endpunkt, Hangfire-Jobs, Ordner-Watcher, IMAP-Poller |
| `postgres` | postgres:17 | Daten, Volltext, Hangfire-Storage |
| `kosit` | KoSIT-Validator (Daemon-Modus) + XRechnung-Konfiguration + xrechnung-visualization (Saxon-HE) | Validierung und HTML-Visualisierung von E-Rechnungen, nur intern erreichbar |
| `db-backup` | kleiner Container mit cron + pg_dump | nächtlicher Dump, 30 Tage rotierend (R3-5) |

### Verzeichnisse (Bind-Mounts auf dem Host)
| Pfad | Samba-Share | Rechte für Benutzer | Rechte für die App |
|---|---|---|---|
| `/srv/rechnungen/eingang` | `Rechnungen-Eingang` | lesen + schreiben (Scanner) | lesen, löschen nach Übernahme |
| `/srv/rechnungen/staging` | – | – | lesen + schreiben (interner Eingangsbereich) |
| `/srv/rechnungen/ablage` | `Rechnungen-Ablage` mit `vfs objects = worm` | nur lesen | schreibt jede Datei genau einmal |
| `/srv/rechnungen/backup` | – | – | db-backup schreibt |

> **Zu prüfen (P2):** Die App schreibt über den Bind-Mount, nicht über SMB. Das Samba-WORM-Modul schützt deshalb den Zugriff über den Share. Gegen Änderungen direkt auf dem Host schützen Hash und nächtliche Integritätsprüfung.

## 3. Tech-Stack

| Bereich | Wahl |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, EF Core + Npgsql, Hangfire (Hangfire.PostgreSql), Serilog |
| E-Rechnung | ZUGFeRD-csharp (ZUGFeRD/Factur-X/XRechnung CII; UBL-Unterstützung der aktuellen Version prüfen, sonst eigener UBL-Parser); PdfPig zum Auslesen eingebetteter XML; Sidecar `kosit` für Validierung und Visualisierung |
| PDF | PdfPig (lesen), PDFsharp (Seiten aufteilen), Rendering der Vorschaubilder/Logo-Ausschnitte über PDFium (Docnet.Core o. ä.) |
| Extraktion | Azure.AI.DocumentIntelligence, Modell `prebuilt-invoice`, EU-Region |
| Mail | MailKit (IMAP, App-Passwort) |
| Auth | ASP.NET Core Identity (Cookie, TOTP), Google OIDC, persönliche API-Tokens für MCP |
| MCP | Offizielles C#-SDK `ModelContextProtocol.AspNetCore`, Streamable HTTP unter `/mcp` |
| Auto-Zuordnung | ML.NET (Textklassifikation auf dem Volltext) |
| DB | PostgreSQL 17, Volltext `german` + `pg_trgm` |
| Frontend | Vite, React, TypeScript, TanStack Query + Router, shadcn/ui + Tailwind, Recharts, react-pdf (PDF.js), Material Icons, vite-plugin-pwa |
| Tests | xUnit + Testcontainers (Postgres), Vitest, Playwright (Smoke-Tests) |

**Abweichung von Q13 (P1):** Das Parsing der E-Rechnung bleibt in-process. Für KoSIT-Validierung (R2-11) und XRechnung-Visualisierung (B2) gibt es dagegen nur Java- bzw. XSLT-2.0/3.0-Werkzeuge, die unter .NET nicht ohne kommerzielle Lizenz (SaxonCS) laufen. Deshalb übernimmt das der kleine Sidecar `kosit`.

## 4. Repository-Struktur (Monorepo, Q30)

```
/backend
  Biber.Invoice.sln
  src/Biber.Invoice.Api           Endpunkte, Auth, MCP, Hosting
  src/Biber.Invoice.Domain        Entitäten, Regeln (Plausibilität, Normalisierung)
  src/Biber.Invoice.Infrastructure  EF Core, Dateiablage, DI-Client, IMAP, KoSIT-Client, ML.NET
  tests/…
/frontend                            Vite-App
/docker
  kosit/Dockerfile
  db-backup/Dockerfile
/docs
  plan.md
  verfahrensdokumentation/
/planung                             Grilling-Fragebögen und Entscheidungsprotokoll
docker-compose.yml
.env.example
```

## 5. Datenmodell (Kern)

```
Beleg
  Id, Richtung (Eingang|Ausgang), Belegart (Rechnung|Gutschrift|Quittung)
  Status (Importiert|InVerarbeitung|Fertig|Fehler), Quelle (Upload|Ordner|Mail), ImportVorgangId
  KorrespondentId?, KategorieId?, Rechnungsnummer, Rechnungsdatum, Leistungsdatum/-zeitraum, Faelligkeitsdatum
  Waehrung, Netto, Steuer, Brutto, ExtraktionsQuelle (XRechnung|ZUGFeRD|DocumentIntelligence|Manuell)
  DiKonfidenz?, Volltext + tsvector, DuplikatVonId?, AufgeteiltAusBelegId?
  ErstelltAm/Von
BelegDatei      Id, BelegId, Rolle (Original|EingebettetesXml|Visualisierung|Vorschau), Pfad, Sha256, Groesse, MimeType,
                AbgelegtAm, LetztePruefungAm, PruefErgebnis
Steuerzeile     BelegId, Satz, Netto, Steuer
Position        BelegId, Nr, Text, TextNormalisiert, Artikelnummer?, Variante?, Menge, Einheit, Einzelpreis, Gesamt, Steuersatz,
                ArtikelId?, Grundpreis?, GrundpreisEinheit?
Artikel         Id, KorrespondentId, Artikelnummer?, NameNormalisiert, StammartikelId?
Stammartikel    Id, Name, Basiseinheit
Korrespondent   Id, Name, Aliase[], UstIdNr, Steuernummer, Ibans[], Anschrift, Website/Domain, Email,
                LogoDateiId?, LogoQuelle (Upload|Pdf|Favicon), DefaultKategorieId?, IstLieferant, IstKunde, Matching
Kategorie       Id, Name, EuerZeile?, Matching
Tag             Id, Name, Farbe, IstInbox, IstZahlungsTag, IstSystem, Matching
BelegTag        BelegId, TagId, GesetztAm/Von
PruefBefund     BelegId, Pruefung, Ergebnis (Ok|Fehlgeschlagen), Detail, Zeitpunkt
ImportVorgang   Id, Quelle, Herkunft (Dateipfad | Mail-Message-Id, Absender, Betreff), Status, Fehler, Versuche
AuditEintrag    Id, Zeitpunkt, BenutzerId|System, Entitaet, EntitaetId, Aktion, Feld, AlterWert, NeuerWert   (append-only)
Benachrichtigung, ApiToken (Hash, Name, Scopes, ZuletztGenutzt, Widerrufen), Einstellung (Key/Value), DiVerbrauch (Monat, Seiten),
MatchingModell (Blob, TrainiertAm), Benutzer/Rollen (Identity; Freigegeben-Flag)

Matching = { Algorithmus: Keiner|EinWort|AlleWoerter|Exakt|Regex|Unscharf|Auto, Muster, GrossKlein }
```

- **Änderungsprotokoll (B7):** Ein EF-Core-`SaveChangesInterceptor` schreibt für jede Änderung an Beleg, Position, Steuerzeile, Tags, Korrespondent und Kategorie einen Audit-Eintrag. Ein DB-Trigger verbietet `UPDATE` und `DELETE` auf `AuditEintrag`.
- **Ausgangsrechnungen (R2-4):** Vorbereitet sind nur `Richtung` sowie `IstLieferant`/`IstKunde`. Das Erstellen kommt später.

## 6. Verarbeitungspipeline

```
Upload ─┐
Ordner ─┼─► ImportVorgang ─► Staging (+SHA-256) ─► Hangfire: Verarbeiten
Mail  ──┘                                            │
                                                     ├─ 1. Dubletten-Check (Hash) ──► Treffer: als Dublette verknüpfen, Ende
                                                     ├─ 2. Format erkennen: XRechnung-XML | PDF mit ZUGFeRD | PDF | Bild
                                                     ├─ 3a. E-Rechnung: XML parsen (maßgeblich, Q14), KoSIT validieren + visualisieren
                                                     ├─ 3b. sonst: Document Intelligence prebuilt-invoice (Seitenlimit, R2-13)
                                                     ├─ 4. Korrespondent zuordnen (USt-IdNr > IBAN > Name/Alias), sonst neu anlegen
                                                     ├─ 5. Positionen → Artikel (über Artikelnummer, sonst normalisierten Text); Grundpreis, falls Menge/Einheit erkannt
                                                     ├─ 6. Fachliche Dublette (Korrespondent + Rechnungsnr. + Betrag)
                                                     ├─ 7. Zuordnung: Default-Kategorie, Matching-Regeln, Auto (ML.NET)
                                                     ├─ 8. Inbox-Tags setzen; Plausibilitätsprüfungen → bei Fehlern Tag „zu prüfen“ + PruefBefund
                                                     └─ 9. Ablage: einmalig nach ablage/{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnr}.{ext}, Staging löschen
```

- **Ordner-Watcher:** `FileSystemWatcher` plus Polling alle 30 s als Absicherung. Übernommen wird eine Datei erst, wenn ihre Größe 10 s lang stabil ist (Scanner schreiben schrittweise). Akzeptiert: PDF, XML, PNG/JPG/TIFF.
- **Mail (Q7/Q8):** IMAP-Polling alle 5 min; verarbeitet werden nur Anhänge (PDF/XML/Bild). Danach wird die Mail in den IMAP-Ordner `Verarbeitet` verschoben, Mails ohne verwertbaren Anhang nach `Ohne Beleg`. Die Absender-Domain wird für das Favicon gemerkt.
- **DI-Fehler (R2-12):** 5 Versuche mit exponentiellem Backoff über ca. 1 h, danach Status `Fehler`, Tag „zu prüfen“, manuelle Erfassung, Button „Erneut auslesen“.
- **DI-Limit (R2-13):** Seitenzähler pro Monat; Benachrichtigung bei 80 %, bei 100 % warten Jobs in einer pausierten Queue.
- **Ablagepfad:** Dateinamen werden bereinigt; bei Kollision kommt `_2`, `_3` dazu. Ist der Korrespondent unbekannt, heißt der Ordner `_Unbekannt`. Der Pfad bleibt fix (R2-6): Spätere Korrekturen ändern nur die DB, die Datei bleibt, wo sie ist.
- **Aufteilen (R3-8):** In der UI Seiten wählen → neuer Beleg. Die Teil-PDFs werden als neue Dateien erzeugt und laufen durch die Pipeline; das ungeteilte Original bleibt unverändert und verknüpft.

### Plausibilitätsprüfungen (R2-11, alle konfigurierbar)
1. Summe der Positionen = Netto bzw. Brutto (Toleranz 0,02 €)
2. Netto + USt = Brutto je Steuersatz
3. Pflichtfelder: Korrespondent, Datum, Rechnungsnummer, Gesamtbetrag
4. Datum nicht in der Zukunft und nicht älter als 2 Jahre
5. §14-UStG-Angaben (Steuernummer/USt-IdNr., Leistungsdatum, Empfänger …); bei Kleinbetragsrechnungen ≤ 250 € reduziert; ausgeblendet bei USt-Status „Kleinunternehmer“
6. Dublettenverdacht
7. Neuer Korrespondent
8. DI-Konfidenz unter Schwelle (Standard 0,8)
9. KoSIT-Validierung fehlgeschlagen

## 7. Tags, Kategorien, Zuordnung (paperless-Stil)

- **Tags** sind frei, mit Farbe. Es gibt drei Sonderformen:
  - **Inbox-Tag:** wird jedem neuen Beleg automatisch gesetzt.
  - **Zahlungs-Tag**, z. B. „bezahlt“, „bezahlt PayPal“: **ohne Datum** (R4-5). „Offen“ heißt: kein Zahlungs-Tag. „Überfällig“ heißt: offen und das Fälligkeitsdatum liegt in der Vergangenheit.
  - **System-Tag „zu prüfen“:** wird nur von der Pipeline gesetzt und nur durch „Bestätigen“ entfernt.
- **Kategorien** sind frei, ohne Vorbelegung. Jede hat ein optionales Feld `EuerZeile` (D2). Die Default-Kategorie hängt am Korrespondenten und ist pro Beleg überschreibbar.
- **Matching (R4-7)** gibt es an Tags, Kategorien und Korrespondenten:
  - Algorithmen wie bei paperless: ein Wort, alle Wörter, exakt, Regex, unscharf, Auto.
  - **Auto:** ML.NET-Klassifikatoren auf dem Volltext, trainiert per nächtlichem Hangfire-Job aus den bisherigen Zuweisungen; der Job läuft erst, wenn genug Daten vorhanden sind.

## 8. Ablage, GoBD, Betrieb

| Thema | Umsetzung |
|---|---|
| Unveränderbarkeit | Share mit WORM (`vfs_worm`) und nur Lesezugriff für Benutzer; SHA-256 pro Datei; nächtlicher Integritätsjob vergleicht die Hashes und erzeugt bei Abweichung eine Benachrichtigung und einen Eintrag im Protokoll |
| Änderungsprotokoll | Append-only, in der Beleg-Detailansicht als „Historie“ sichtbar |
| Aufbewahrung | Löschsperre bis Ende des 8. Jahres nach dem Belegjahr (konfigurierbar). Danach erscheint der Beleg in einer Liste „Löschung möglich“. Die App entfernt die DB-Daten; die Datei auf dem WORM-Share löscht der Admin auf dem Host |
| Backup | `db-backup`: pg_dump täglich 02:00 nach `/srv/rechnungen/backup`, 30 Tage. Ablage und Backup-Ordner sichert das vorhandene Backup (P3) |
| Verfahrensdoku | `docs/verfahrensdokumentation/` (Entwurf liegt bei) |
| Netzwerk | Nginx Proxy Manager: Proxy-Host `rechnungen.<domain>` → `api:8080`; Let's Encrypt per DNS-Challenge mit IONOS (oder vorhandenes Wildcard-Zertifikat); Access-List nur LAN. Der DNS-Eintrag zeigt auf die LAN-IP |
| Konfiguration | `.env` (Azure-Key, IMAP-Zugang, DB-Passwort); Fachliches (Schwellen, Limits, USt-Status) über die Einstellungsseite in der DB |

## 9. Authentifizierung und Rollen

- **Lokale Konten:** ASP.NET Core Identity mit Cookie-Auth (SPA vom selben Origin), Passwort und TOTP-2FA.
- **Google-Login:** über OIDC. Die Redirect-URL liegt unter der LAN-Domain; Google muss den Server dafür nicht erreichen.
- **Registrierung (R2-16):** offen. Neue Konten sind gesperrt, bis ein Admin sie freigibt; dazu erscheint ein Eintrag in der Glocke. Der erste Benutzer wird automatisch Admin.
- **Rollen:**
  - **Admin:** Benutzer, Einstellungen, Löschfreigaben
  - **Benutzer:** alles bearbeiten
  - **Nur-Lesen**
- **API-Tokens für MCP:** in der UI erzeugt, nur als Hash gespeichert, widerrufbar, Scopes `lesen` und `upload`.

## 10. MCP-Server (Q24/Q25/R2-18)

Streamable HTTP unter `https://rechnungen.<domain>/mcp`, Auth per `Authorization: Bearer <API-Token>`. Alle Tools nutzen dieselben Application-Services wie die REST-API (A5).

| Tool | Zweck |
|---|---|
| `belege_suchen` | Volltext und Filter (Zeitraum, Korrespondent, Kategorie, Tag, Betrag) |
| `beleg_abrufen` | Kopfdaten, Positionen, Tags, Prüfbefunde, Link |
| `kosten_auswerten` | Summen nach Monat, Kategorie oder Korrespondent für einen Zeitraum |
| `korrespondent_abrufen` | Stammdaten, Gesamtkosten, letzte Belege |
| `artikel_suchen` / `artikel_preisverlauf` | Artikel bzw. Stammartikel und Preisentwicklung |
| `beleg_hochladen` | Datei (Base64) → Pipeline |

OAuth (OpenIddict) für claude.ai-Connectoren kommt später; ohne Zugriff von außen ist er ohnehin nur eingeschränkt nutzbar.

## 11. Frontend

| Seite | Inhalt |
|---|---|
| Dashboard | Kosten pro Monat (gestapelt nach Kategorie), Vorjahresvergleich, Top-Korrespondenten, größte Preisänderungen, offene/überfällige Belege, Inbox und „zu prüfen“ mit Gründen, Vorsteuer je Zeitraum (nur bei Regelbesteuerung bzw. „unbekannt“), DI-Verbrauch. Zeitbasis: immer Rechnungsdatum (R4-5) |
| Belege | Tabelle mit Filtern, Sortierung und Volltextsuche; Massenaktionen (Tags, Kategorie, bestätigen) |
| Beleg-Detail | PDF-Viewer bzw. XRechnung-Visualisierung neben den Daten; Positionen, Steuerzeilen, Tags, Prüfbefunde, Historie, Dubletten, „Aufteilen“, „Erneut auslesen“ |
| Upload | Drag & Drop, mehrere Dateien |
| Korrespondenten | Liste mit Logo und Gesamtkosten; Detail mit Anschrift, Kostenverlauf und Belegen; Zusammenführen, Logo hochladen, Matching |
| Artikel | Liste; Detail mit Preisverlauf (Einzel- und Grundpreis) und Belegen; zu Stammartikeln zusammenführen |
| Tags / Kategorien | Verwaltung inkl. Matching und Sonderformen |
| Export | Zeitraum wählen → Excel/CSV-Belegliste, Summen je Kategorie (+ EÜR-Zeile), Vorsteuer je UStVA-Zeitraum, ZIP der Originale. **Ohne Zahlungsdatum** (R4-5) |
| Einstellungen | Allgemein, USt-Status, Prüfungen und Schwellen, DI-Limit, Mail, Aufbewahrung, Benutzer und Freigaben, API-Tokens, Integritätsstatus |
| Glocke | In-App-Benachrichtigungen (D5) |

**Layout:** responsive, Desktop mit Seitenleiste, Mobile mit Bottom-Navigation.
**PWA:** installierbar, App-Shell offline, zuletzt geladene Listen gecacht, Upload-Warteschlange (Background Sync), Share-Target für PDFs unter Android.

## 12. Ausbaustufen

Jede Phase endet mit einer lauffähigen, per `docker compose up` startbaren Version.

**Phase 0 – Grundgerüst**
- Repo, Solution und Vite-App; docker-compose mit api, postgres, kosit, db-backup
- EF-Core-Migrationen, Healthchecks, Serilog
- GitHub Actions für Build und Tests

**Phase 1 – MVP: Belege rein, sauber abgelegt, auffindbar**
- Import per Upload und Eingangsordner; Staging, Hash, Dubletten
- E-Rechnung (XML/ZUGFeRD) inkl. KoSIT; Document Intelligence inkl. Retry und Limit
- Plausibilitätsprüfungen, Tag „zu prüfen“, Bestätigen
- WORM-Ablage und Integritätsjob
- Belegliste, Suche, Detailansicht, manuelle Korrektur, Aufteilen
- Korrespondenten (automatische Erkennung, Zusammenführen, Anschrift)
- Login mit Passwort + TOTP; der erste Benutzer ist Admin, der Admin legt Benutzer an
- Änderungsprotokoll und Löschsperre
- Einstellungsseite (Basis)
- *Abnahme:* Eine gescannte PDF-Rechnung, eine ZUGFeRD-Rechnung und eine XRechnung landen korrekt ausgelesen in der Ablage. Eine fehlerhafte Rechnung bekommt „zu prüfen“ mit Grund. Jede Korrektur steht in der Historie.

**Phase 2 – Alltag und Auswertung**
- Mail-Import per IMAP
- Tags mit Inbox- und Zahlungs-Tags; freie Kategorien mit EÜR-Zeile; Matching-Regeln (ohne Auto)
- Export
- Dashboard (alle Kacheln außer Artikel)
- Glocke (vorgezogen aus Phase 4, P4)

**Phase 3 – Artikel, Logos, KI-Anbindung**
- Artikel, Stammartikel, Grundpreis, Preisverlauf und die Dashboard-Kachel dazu
- Logos (PDF-Ausschnitt, Upload, Favicon)
- MCP-Server mit API-Tokens
- Auto-Matching mit ML.NET

**Phase 4 – Mobil und Benutzer**
- PWA (Offline, Upload-Warteschlange, Share-Target)
- Google-Login
- Offene Registrierung mit Freigabe
- Mobile-Feinschliff

**Später**
- OAuth für MCP (claude.ai-Connector)
- Ausgangsrechnungen (Erstellen, Nummernkreise, ZUGFeRD-Erzeugung)
- Mailtext als Beleg, Download-Links folgen
- Fremdwährungsumrechnung

## 13. Offene Punkte zur Prüfung

| # | Punkt | Mein Vorschlag |
|---|---|---|
| **P1** | KoSIT-Validierung und XRechnung-Visualisierung brauchen Java bzw. XSLT 3.0, das weicht von „in-process“ (Q13) ab | Sidecar `kosit` wie oben; das Parsing bleibt in .NET |
| **P2** | Die App schreibt per Bind-Mount, WORM greift nur über SMB | Akzeptieren, Absicherung über Hash und Integritätsjob. Alternativ schreibt die App über einen eigenen SMB-Benutzer auf den WORM-Share (aufwendiger) |
| **P3** | „NAS“ ist ein Samba-Container auf demselben Rechner. Gibt es ein Backup **auf ein anderes Gerät**? Ohne das ist GoBD-Aufbewahrung bei einem Hardwaredefekt nicht gesichert | Bitte bestätigen, wohin das vorhandene Backup sichert |
| **P4** | Die Glocke wird für Integritätsfehler und DI-Limit früh gebraucht | Von Phase 4 in Phase 2 vorgezogen |
| **P5** | Das Host-Betriebssystem ist unbekannt (Linux vs. Docker Desktop unter Windows) | Watcher mit Polling-Absicherung funktioniert auf beiden; Pfade in `.env` konfigurierbar |
| **P6** | USt-Status unbekannt | Einstellung mit Standard „unbekannt“ (Vorsteuer-Ansichten sichtbar); bitte klären |

## 14. Risiken

- **Extraktion von Positionen bei unüblichen Layouts:** Document Intelligence liefert Positionen nicht immer vollständig. Das fängt die Prüfung „Summe der Positionen“ auf; Artikelstatistiken bleiben trotzdem lückenhaft.
- **Fester Ablagepfad:** Ein falsch erkannter Korrespondent bleibt im Dateipfad stehen (bewusst so, R2-6). Maßgeblich sind die Daten in der DB.
- **Auto-Matching:** braucht einige hundert Belege, bevor es zuverlässig wird. Bis dahin greifen Regeln und Default-Kategorien.
