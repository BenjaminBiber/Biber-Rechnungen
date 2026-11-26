# Verfahrensdokumentation: Digitale Belegverwaltung „Biber-Rechnungen“

Version: 1.0
Stand: 2026-11-24

> Gliederung angelehnt an die Muster-Verfahrensdokumentation zur Belegablage (AWV/BStBK) und die GoBD (Rz. 151 ff.).
> Stellen mit **[ERGÄNZEN: …]** muss der Unternehmer ausfüllen; sie sind am Ende unter „Vom Betreiber zu ergänzen“ gesammelt. Technische Abschnitte werden mit der Software mitversioniert und bei jeder relevanten Änderung aktualisiert (siehe Abschnitt 7). Einrichtung und Betrieb im Detail: [Betriebsdokumentation](../betrieb.md).

| Version | Datum | Änderung | Verantwortlich |
| --- | --- | --- | --- |
| 0.1 | 2026-11-23 | Erstentwurf | [ERGÄNZEN: Name] |
| 1.0 | 2026-11-24 | Technische Abschnitte an die Umsetzung angeglichen | [ERGÄNZEN: Name] |

---

## 1. Allgemeine Beschreibung

### 1.1 Unternehmen

- Firma, Rechtsform, Anschrift: **[ERGÄNZEN: Firma, Rechtsform, Anschrift]**
- Steuernummer / USt-IdNr.: **[ERGÄNZEN: Steuernummer, USt-IdNr.]**
- Umsatzsteuerlicher Status (Regelbesteuerung / Kleinunternehmer § 19 UStG): **[ERGÄNZEN: USt-Status]**; derselbe Wert ist auf der Einstellungsseite hinterlegt.
- Gewinnermittlung: Einnahmen-Überschuss-Rechnung (§ 4 Abs. 3 EStG)
- Buchführung: durch den Unternehmer selbst (Excel/ELSTER). „Biber-Rechnungen“ ist ein vorgelagertes Belegarchiv, kein Buchführungssystem.

### 1.2 Zweck und Umfang des Verfahrens

Das Verfahren regelt Empfang, Erfassung, Prüfung, Archivierung und Auswertung **eingehender geschäftlicher Belege**: Rechnungen, Gutschriften/Stornos und sonstige Quittungen. Private Belege und Kassenbons werden nicht erfasst. Ausgangsrechnungen werden nicht mit dem System erstellt.

Stichtag des Verfahrens: **[ERGÄNZEN: Stichtag, z. B. 01.01.2027]**. Belege vor dem Stichtag bleiben in der bisherigen Papierablage und unterliegen dieser Dokumentation nicht.

### 1.3 Beteiligte Personen und Zuständigkeiten

| Rolle | Person | Aufgaben |
| --- | --- | --- |
| Verantwortlicher / Admin | [ERGÄNZEN: Admin] | Freigabe von Benutzern, Einstellungen, Kontrolle der Integritätsmeldungen, Löschfreigaben, Pflege dieser Dokumentation |
| Benutzer | [ERGÄNZEN: Benutzer] | Belege hochladen, prüfen, korrigieren und verschlagworten |
| Nur-Lesen (optional) | [ERGÄNZEN: z. B. Steuerberater] | Einsicht |
| Technischer Betrieb | [ERGÄNZEN: Technischer Betrieb] | Server, Updates, Backup |

## 2. Anwenderdokumentation

### 2.1 Belegeingang

| Weg | Beschreibung |
| --- | --- |
| E-Mail | Rechnungen gehen an das Postfach **[ERGÄNZEN: Rechnungsadresse]**. Das System ruft das Postfach alle 5 Minuten per IMAP ab und übernimmt Anhänge (PDF, XML, Bilder). Verarbeitete Mails werden in den IMAP-Ordner `Verarbeitet`, Mails ohne verwertbaren Anhang nach `Ohne Beleg` verschoben, nicht gelöscht. |
| Papier | Papierrechnungen werden mit dem Scanner **[ERGÄNZEN: Scanner-Gerät]** in die Netzwerkfreigabe `Rechnungen-Eingang` gescannt. **Das Papieroriginal wird zusätzlich aufbewahrt** (kein ersetzendes Scannen). Ablageort: **[ERGÄNZEN: Ablageort Papier]**. |
| Upload | Dateien werden über die Weboberfläche (auch als installierte App) hochgeladen. |
| Programmzugriff | Über die MCP-Schnittstelle (Abschnitt 3.7) mit dem Werkzeug `beleg_hochladen`. |

Erfasst wird möglichst zeitnah, spätestens **[ERGÄNZEN: Frist, z. B. innerhalb von 10 Tagen]** nach Eingang.

### 2.2 Elektronische Rechnungen (E-Rechnung)

Bei XRechnung (XML) und ZUGFeRD/Factur-X (PDF mit eingebettetem XML) ist **der strukturierte XML-Datensatz das steuerlich maßgebliche Original**. Er wird unverändert archiviert. Die Bildansicht (Visualisierung) wird für die Anzeige erzeugt und ist kein Original.

### 2.3 Erfassung und Prüfung

Die automatische Verarbeitung ist in Abschnitt 3.2 beschrieben. Belege, bei denen eine Prüfung (Abschnitt 3.3) fehlschlägt, erhalten den Tag „zu prüfen“ mit Begründung. Ein Benutzer prüft den Beleg gegen das Original, korrigiert ihn bei Bedarf und bestätigt ihn. Neue Belege tragen zusätzlich den Inbox-Tag, bis ein Benutzer sie gesichtet hat.

### 2.4 Korrekturen

Ausgelesene Daten können jederzeit korrigiert werden. **Jede Änderung wird mit Zeitpunkt, Benutzer, altem und neuem Wert protokolliert** (Abschnitt 3.5). Das Original bleibt von Korrekturen unberührt.

### 2.5 Übergabe an die Buchführung

Der Export für einen Zeitraum enthält eine Belegliste (Excel/CSV), Summen je Kategorie (mit EÜR-Zeile), die Vorsteuer je Voranmeldungszeitraum und eine ZIP-Datei der Originale. Auf dieser Grundlage wird die Buchführung erstellt. Rhythmus: **[ERGÄNZEN: Rhythmus, z. B. monatlich/quartalsweise]**.

## 3. Technische Systemdokumentation

### 3.1 Systemübersicht

- **Hardware/Betriebssystem:** [ERGÄNZEN: Gerät, Betriebssystem]. Standort: [ERGÄNZEN: Standort]
- **Software:** „Biber-Rechnungen“, Version laut Git-Tag. Die Anwendung läuft mit Docker Compose in vier Diensten:

| Dienst | Inhalt | Aufgabe |
| --- | --- | --- |
| `api` | .NET 10 mit ausgeliefertem Web-Frontend | Weboberfläche, REST-API, MCP-Endpunkt, Hintergrundjobs (Hangfire), Ordner-Watcher, IMAP-Abruf |
| `postgres` | PostgreSQL 17 | ausgelesene Daten, Änderungsprotokoll, Volltextsuche, Job-Speicher |
| `kosit` | KoSIT-Validator mit XRechnung-Konfiguration und Visualisierung | Validierung und Anzeige von E-Rechnungen, nur im internen Netz erreichbar |
| `db-backup` | PostgreSQL-Werkzeuge | nächtliche Datenbanksicherung |

- **Vorhandene Infrastruktur des Betreibers:** ein Samba-Container stellt die Freigaben `Rechnungen-Eingang` und `Rechnungen-Ablage` bereit; Nginx Proxy Manager stellt HTTPS bereit.
- **Netzwerk:** Das System ist nur im lokalen Netz erreichbar (HTTPS, Zugriffsliste nur LAN). Ausgehende Verbindungen gehen nur zum Mailserver, zu Azure Document Intelligence (EU-Region), zu Google (Anmeldung), zu Webseiten von Lieferanten (Logos) und zu VIES der EU-Kommission (Prüfung der USt-IdNr. von Lieferanten; abschaltbar, übermittelt wird nur die Nummer).

### 3.2 Verarbeitungsschritte

1. **Import:** Upload, Eingangsordner oder E-Mail erzeugen einen Importvorgang. Die Datei wird in die Zwischenablage (Staging) kopiert und ihre SHA-256-Prüfsumme berechnet. Dateien im Eingangsordner werden erst übernommen, wenn ihre Größe 10 Sekunden stabil ist, und danach aus dem Eingang gelöscht.
2. **Dublettenprüfung** über die Prüfsumme: identische Dateien werden als Dublette verknüpft und nicht erneut abgelegt.
3. **Format erkennen:** XRechnung-XML, PDF mit ZUGFeRD, sonstiges PDF oder Bild.
4. **Auslesen:** E-Rechnungen aus dem XML (maßgeblich), Validierung und Visualisierung über `kosit`; sonstige Belege über Azure Document Intelligence (Modell `prebuilt-invoice`, EU-Region). Fehlschläge werden fünfmal über etwa eine Stunde wiederholt, danach erhält der Beleg den Status „Fehler“ und „zu prüfen“. Steht auf einer Rechnung kein Fälligkeitsdatum, wird es aus den Zahlungsbedingungen im Text abgeleitet (z. B. „zahlbar innerhalb von 14 Tagen“ ab Rechnungsdatum; Skonto-Fristen zählen nicht).
5. **Korrespondent zuordnen** (USt-IdNr., IBAN, Name/Alias), sonst neu anlegen.
6. **Positionen** normalisieren und Artikeln zuordnen.
7. **Fachliche Dublette** prüfen (Korrespondent, Rechnungsnummer, Betrag).
8. **Zuordnung** von Kategorie und Tags (Standardkategorie, Regeln, automatische Zuordnung).
9. **Prüfungen** (Abschnitt 3.3), Inbox-Tag und bei Befunden „zu prüfen“.
10. **Ablage** (Abschnitt 3.4), danach wird die Datei aus dem Staging gelöscht.

Ein Beleg kann seitenweise **aufgeteilt** werden: Die Teile entstehen als neue Dateien und durchlaufen die Verarbeitung; das ungeteilte Original bleibt unverändert und verknüpft.

### 3.3 Prüfungen

Folgende elf Plausibilitätsprüfungen laufen für jeden Beleg; jede ist auf der Einstellungsseite ein- und ausschaltbar. Jedes Ergebnis wird als Prüfbefund mit Zeitpunkt gespeichert.

| Nr. | Prüfung | Inhalt |
| --- | --- | --- |
| 1 | Positionssumme | Summe der Positionen = Netto bzw. Brutto (Toleranz 0,02 €) |
| 2 | Netto + Steuer = Brutto | je Steuersatz |
| 3 | Pflichtfelder | Korrespondent, Datum, Rechnungsnummer, Gesamtbetrag |
| 4 | Datum | nicht in der Zukunft und nicht älter als 2 Jahre |
| 5 | § 14 UStG | Pflichtangaben; bei Kleinbetragsrechnungen bis 250 € reduziert; entfällt bei Kleinunternehmer-Status |
| 6 | Dublettenverdacht | gleiche Datei oder gleicher Korrespondent, Rechnungsnummer und Betrag |
| 7 | Neuer Korrespondent | erster Beleg eines Lieferanten |
| 8 | Erkennungssicherheit | Konfidenz von Document Intelligence unter Schwelle (Standard 0,8) |
| 9 | KoSIT-Validierung | E-Rechnung verletzt die XRechnung-Regeln |
| 10 | Bankverbindung bekannt | IBAN der Eingangsrechnung weicht von den beim Korrespondenten hinterlegten ab; sie wird erst mit dem Bestätigen des Belegs hinterlegt |
| 11 | USt-IdNr. gültig | USt-IdNr. des Korrespondenten ist laut VIES (EU-Kommission) ungültig; geprüft wird im Hintergrund und halbjährlich erneut, eine ungültige Nummer meldet zusätzlich die Glocke |

### 3.4 Ablagepfad und Unveränderbarkeit

- **Originale** liegen im Ablageverzeichnis unter `{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnummer}.{Endung}`. Dateinamen werden bereinigt, bei Namensgleichheit wird `_2`, `_3` angehängt, bei unbekanntem Korrespondenten heißt der Ordner `_Unbekannt`. Das System schreibt jede Datei **genau einmal**; der Pfad bleibt fest, spätere Korrekturen ändern nur die Datenbank.
- **Schreibschutz über die Freigabe:** `Rechnungen-Ablage` ist für Benutzer schreibgeschützt und zusätzlich mit dem Samba-Modul `vfs_worm` (ohne Karenzzeit) gegen Ändern und Löschen gesichert.
- **Schutz auf dem Host:** Die Anwendung schreibt über einen Bind-Mount, nicht über die Freigabe. Gegen Änderungen direkt auf dem Host sichern deshalb Prüfsummen ab: Für jede Datei wird beim Ablegen eine SHA-256-Prüfsumme gespeichert. Ein **nächtlicher Integritätslauf** vergleicht alle Dateien mit ihrer Prüfsumme; Abweichungen und fehlende Dateien erzeugen eine Benachrichtigung an den Admin und einen Protokolleintrag. Der Admin kann den Lauf jederzeit manuell starten.
- **Daten:** Ausgelesene Daten liegen in der PostgreSQL-Datenbank.

### 3.5 Änderungsprotokoll

Jede Änderung an Beleg, Position, Steuerzeile, Tags, Korrespondent und Kategorie erzeugt automatisch einen Protokolleintrag mit Zeitpunkt, Benutzer (oder „System“), Objekt, Feld, altem und neuem Wert. Ein Datenbank-Trigger verbietet das Ändern und Löschen von Protokolleinträgen. Das Protokoll ist je Beleg als „Historie“ sichtbar.

### 3.6 Berechtigungen und Rollen

- Persönliche Benutzerkonten mit Passwort und Zwei-Faktor-Authentifizierung (TOTP) oder Google-Konto.
- Das erste Konto wird bei der Ersteinrichtung automatisch Admin. Weitere Konten können sich registrieren, sind aber gesperrt, bis ein Admin sie freigibt.
- Rollen:

| Rolle | Rechte |
| --- | --- |
| Admin | alles, zusätzlich Benutzer und Freigaben, Einstellungen, Löschfreigaben, Integritätsprüfung |
| Benutzer | Belege hochladen, bearbeiten, bestätigen, Stammdaten pflegen |
| Nur-Lesen | Einsicht und Export |

- Programmzugriff (MCP): persönliche, widerrufbare API-Tokens, nur als Hash gespeichert, mit den Scopes `lesen` und `upload`.

### 3.7 Schnittstellen

| Schnittstelle | Richtung | Zweck |
| --- | --- | --- |
| SMB-Freigabe `Rechnungen-Eingang` | eingehend | Scanner und Benutzer legen Belege ab |
| SMB-Freigabe `Rechnungen-Ablage` | ausgehend, nur lesen | Einsicht in die Originale |
| IMAP | ausgehend | Abruf des Rechnungspostfachs |
| Azure Document Intelligence | ausgehend | Auslesen von Belegen ohne E-Rechnung |
| VIES (EU-Kommission) | ausgehend | Gültigkeit der USt-IdNr. von Lieferanten (abschaltbar) |
| KoSIT (`kosit`) | intern | Validierung und Visualisierung von E-Rechnungen |
| REST-API | eingehend (LAN) | Weboberfläche |
| MCP unter `/mcp` | eingehend (LAN) | Zugriff für KI-Assistenten mit API-Token |
| Export | ausgehend | Belegliste, Summen, Vorsteuer, ZIP der Originale |

Die MCP-Schnittstelle bietet sieben Werkzeuge; alle nutzen dieselben Dienste und Berechtigungen wie die Weboberfläche und ändern keine Belegdaten außer durch Hochladen:

| Werkzeug | Zweck | Scope |
| --- | --- | --- |
| `belege_suchen` | Volltext und Filter | lesen |
| `beleg_abrufen` | Kopfdaten, Positionen, Tags, Prüfbefunde | lesen |
| `kosten_auswerten` | Summen nach Monat, Kategorie oder Korrespondent | lesen |
| `korrespondent_abrufen` | Stammdaten, Gesamtkosten, letzte Belege | lesen |
| `artikel_suchen` | Artikel und Stammartikel | lesen |
| `artikel_preisverlauf` | Preisentwicklung eines Artikels | lesen |
| `beleg_hochladen` | Datei in die Verarbeitung geben | upload |

### 3.8 Maschinelle Auswertbarkeit / Datenzugriff (§ 147 Abs. 6 AO)

Die Finanzverwaltung erhält Zugriff über einen Nur-Lesen-Benutzer (Z1) oder über den Export der Belegliste und der Originale (Z3).

## 4. Betriebsdokumentation

### 4.1 Datensicherung

- Der Dienst `db-backup` sichert die Datenbank täglich um 02:00 Uhr (`pg_dump`, geprüft mit `pg_restore --list`) in den Backup-Ordner und löscht Sicherungen nach 30 Tagen.
- Backup-Ordner und Ablage werden vom vorhandenen Host-Backup auf ein anderes Gerät gesichert: **[ERGÄNZEN: Backup-Lösung, Zielgerät/-ort, Rhythmus, Aufbewahrung]**.
- Die Wiederherstellung ist in der [Betriebsdokumentation](../betrieb.md) Schritt für Schritt beschrieben (Dump einspielen, Ablage zurückspielen, Integritätsprüfung starten).
- **Wiederherstellungstest:** **[ERGÄNZEN: Rhythmus Wiederherstellungstest, z. B. jährlich]**, Ergebnis wird in Anhang A protokolliert.

### 4.2 Aufbewahrung und Löschung

- Aufbewahrungsfrist für Buchungsbelege: 8 Jahre ab Ende des Kalenderjahres (§ 147 AO in der Fassung ab 2025). Das System sperrt Belege bis Fristende gegen Löschung; die Frist ist auf der Einstellungsseite hinterlegt.
- Nach Fristende erscheint der Beleg in der Liste „Löschung möglich“. Gelöscht wird nur nach Freigabe durch den Admin; die Anwendung entfernt die Daten aus der Datenbank und protokolliert die Löschung. Die Datei auf der WORM-Ablage löscht der Admin anschließend auf dem Host.
- **Falsch zugeordnete Belege:** Gehört ein Beleg nicht in dieses Archiv (z. B. versehentlich hochgeladen oder die Rechnung eines anderen Unternehmens), kann ihn ausschließlich der Admin vor Fristende löschen – nur mit dem Grund „falsch zugeordnet“ und optionaler Bemerkung. Dabei wird nichts vernichtet: Der Beleg wird ausgeblendet (Belege, Auswertungen, Exporte), seine Originaldatei mit unverändertem Inhalt und Hash in der Ablage nach `_Geloescht/` verschoben. Unter Einstellungen → Gelöschte Belege ist er einsehbar und wiederherstellbar. Löschung und Wiederherstellung stehen mit Benutzer, Zeitpunkt und Bemerkung im Änderungsprotokoll. Nach Ende seiner regulären Frist wird er mit seinem Jahrgang endgültig gelöscht.
- Papieroriginale werden nach denselben Fristen aufbewahrt.

### 4.3 Zugriffsschutz

Siehe Abschnitt 3.6. Zusätzlich ist die Anwendung nur aus dem LAN erreichbar; Zugangsdaten für Datenbank, Postfach, Azure und Google liegen ausschließlich in der Konfigurationsdatei `.env` auf dem Server.

### 4.4 Programmänderungen

Der Quellcode wird in Git versioniert. Jede Version in Produktion ist ein Git-Tag. Datenbankänderungen laufen beim Start automatisch als Migration. Änderungen, die Erfassung, Prüfung oder Ablage betreffen, werden in dieser Dokumentation im Änderungsverlauf vermerkt.

## 5. Internes Kontrollsystem (IKS)

| Kontrolle | Häufigkeit | Durchführung |
| --- | --- | --- |
| Inbox und „zu prüfen“ abarbeiten | [ERGÄNZEN: Häufigkeit Inbox, z. B. wöchentlich] | Benutzer |
| Integritätsmeldungen kontrollieren | bei Meldung | Admin |
| Vollständigkeit: Kontoauszüge gegen Belege abgleichen | [ERGÄNZEN: Häufigkeit Abgleich, z. B. monatlich] | [ERGÄNZEN: zuständige Person Abgleich] |
| Benutzer und Tokens überprüfen | jährlich | Admin |
| Backup-Wiederherstellung testen | [ERGÄNZEN: Häufigkeit Wiederherstellungstest] | Technischer Betrieb |

## 6. Auslagerung / Dienstleister

| Dienst | Anbieter | Zweck | Datenstandort |
| --- | --- | --- | --- |
| Azure Document Intelligence | Microsoft | Auslesen von Belegen | EU-Region [ERGÄNZEN: Azure-Region] |
| E-Mail-Postfach | [ERGÄNZEN: Mail-Anbieter] | Empfang von Rechnungen | [ERGÄNZEN: Standort Mail-Anbieter] |
| Google-Anmeldung (optional) | Google | Anmeldung von Benutzern | keine Belegdaten |

## 7. Änderungsverlauf dieser Dokumentation

Siehe Tabelle am Anfang. Die Dokumentation liegt im Repository unter `docs/verfahrensdokumentation/` und ist dadurch mitversioniert; die Anwendung zeigt sie read-only an.

---

### Anhang A – Protokoll der Wiederherstellungstests

| Datum | Getestet | Ergebnis | Durchgeführt von |
| --- | --- | --- | --- |
| | | | |

## Vom Betreiber zu ergänzen

- Verantwortliche Person im Änderungsverlauf
- Firma, Rechtsform, Anschrift; Steuernummer, USt-IdNr.; USt-Status (1.1)
- Stichtag des Verfahrens (1.2)
- Personen für Admin, Benutzer, Nur-Lesen, technischen Betrieb (1.3)
- Rechnungsadresse (E-Mail), Scanner-Gerät, Ablageort Papier, Erfassungsfrist (2.1)
- Rhythmus der Übergabe an die Buchführung (2.5)
- Gerät, Betriebssystem, Standort des Servers (3.1)
- Ziel des Host-Backups: Backup-Lösung, Zielgerät/-ort, Rhythmus, Aufbewahrung (4.1)
- Rhythmus des Wiederherstellungstests (4.1, 5)
- Häufigkeiten und zuständige Person im IKS (5)
- Azure-Region, Mail-Anbieter und dessen Standort (6)
