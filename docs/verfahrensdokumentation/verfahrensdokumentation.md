# Verfahrensdokumentation: Digitale Belegverwaltung „Biber-Rechnungen“

> **Entwurf**, Stand 2026-11-23. Gliederung angelehnt an die Muster-Verfahrensdokumentation zur Belegablage (AWV/BStBK) und die GoBD (Rz. 151 ff.).
> Stellen mit **[ERGÄNZEN]** muss der Unternehmer ausfüllen. Technische Abschnitte werden mit der Software mitversioniert und bei jeder relevanten Änderung aktualisiert (siehe Abschnitt 7).

| Version | Datum | Änderung | Verantwortlich |
|---|---|---|---|
| 0.1 | 2026-11-23 | Erstentwurf | [ERGÄNZEN] |

---

## 1. Allgemeine Beschreibung

### 1.1 Unternehmen
- Firma, Rechtsform, Anschrift: **[ERGÄNZEN]**
- Steuernummer / USt-IdNr.: **[ERGÄNZEN]**
- Umsatzsteuerlicher Status (Regelbesteuerung / Kleinunternehmer §19 UStG): **[ERGÄNZEN]**
- Gewinnermittlung: Einnahmen-Überschuss-Rechnung (§ 4 Abs. 3 EStG)
- Buchführung: durch den Unternehmer selbst (Excel/ELSTER). „Biber-Rechnungen“ ist ein vorgelagertes Belegarchiv, kein Buchführungssystem.

### 1.2 Zweck und Umfang des Verfahrens
Das Verfahren regelt Empfang, Erfassung, Prüfung, Archivierung und Auswertung **eingehender geschäftlicher Belege**: Rechnungen, Gutschriften/Stornos und sonstige Quittungen. Private Belege und Kassenbons werden nicht erfasst. Ausgangsrechnungen werden derzeit nicht mit dem System erstellt.

Stichtag des Verfahrens: **[ERGÄNZEN, z. B. 01.01.2027]**. Belege vor dem Stichtag bleiben in der bisherigen Papierablage und unterliegen dieser Dokumentation nicht.

### 1.3 Beteiligte Personen und Zuständigkeiten
| Rolle | Person | Aufgaben |
|---|---|---|
| Verantwortlicher / Admin | [ERGÄNZEN] | Freigabe von Benutzern, Einstellungen, Kontrolle der Integritätsmeldungen, Löschfreigaben, Pflege dieser Dokumentation |
| Benutzer | [ERGÄNZEN] | Belege hochladen, prüfen, korrigieren und verschlagworten |
| Nur-Lesen (optional) | [ERGÄNZEN, z. B. Steuerberater] | Einsicht |
| Technischer Betrieb | [ERGÄNZEN] | Server, Updates, Backup |

## 2. Anwenderdokumentation

### 2.1 Belegeingang
| Weg | Beschreibung |
|---|---|
| E-Mail | Rechnungen gehen an das Postfach **[ERGÄNZEN: rechnungen@…]**. Das System ruft das Postfach regelmäßig per IMAP ab und übernimmt Anhänge (PDF, XML, Bilder). Verarbeitete Mails werden in einen IMAP-Ordner verschoben und nicht gelöscht. |
| Papier | Papierrechnungen werden mit dem Scanner **[ERGÄNZEN: Gerät]** in den Netzwerkordner „Rechnungen-Eingang“ gescannt. **Das Papieroriginal wird zusätzlich aufbewahrt** (kein ersetzendes Scannen). Ablageort: **[ERGÄNZEN]**. |
| Upload | Dateien werden über die Weboberfläche hochgeladen. |

Erfasst wird möglichst zeitnah, spätestens **[ERGÄNZEN, z. B. innerhalb von 10 Tagen]** nach Eingang.

### 2.2 Elektronische Rechnungen (E-Rechnung)
Bei XRechnung (XML) und ZUGFeRD/Factur-X (PDF mit eingebettetem XML) ist **der strukturierte XML-Datensatz das steuerlich maßgebliche Original**. Er wird unverändert archiviert. Die Bildansicht wird für die Anzeige erzeugt und ist kein Original.

### 2.3 Erfassung und Prüfung
1. Das System liest Belege automatisch aus: E-Rechnungen aus dem XML, sonstige Belege mit Azure Document Intelligence (Microsoft, Rechenzentrum EU).
2. Automatische Plausibilitätsprüfungen: Summen, Steuerberechnung, Pflichtfelder, Datum, Pflichtangaben nach § 14 UStG, Dubletten, neuer Lieferant, Erkennungssicherheit, Validierung der E-Rechnung.
3. Belege, bei denen eine Prüfung fehlschlägt, erhalten den Status „zu prüfen“ mit Begründung. Ein Benutzer prüft den Beleg gegen das Original, korrigiert ihn bei Bedarf und bestätigt ihn.
4. Neue Belege erhalten zusätzlich den Status „Inbox“, bis ein Benutzer sie gesichtet hat.

### 2.4 Korrekturen
Ausgelesene Daten können jederzeit korrigiert werden. **Jede Änderung wird mit Zeitpunkt, Benutzer, altem und neuem Wert protokolliert.** Das Protokoll lässt sich nicht ändern oder löschen und ist je Beleg einsehbar. Das Original bleibt von Korrekturen unberührt.

### 2.5 Übergabe an die Buchführung
Der Export für einen Zeitraum enthält eine Belegliste (Excel/CSV), Summen je Kategorie und eine ZIP-Datei der Originale. Auf dieser Grundlage wird die Buchführung erstellt. **[ERGÄNZEN: Rhythmus, z. B. monatlich/quartalsweise]**

## 3. Technische Systemdokumentation

### 3.1 Systemübersicht
- **Hardware/Betriebssystem:** [ERGÄNZEN: Gerät, OS]. Standort: [ERGÄNZEN]
- **Software:** „Biber-Rechnungen“, Version laut Git-Tag; Komponenten laut `docs/plan.md`, Abschnitt 2:
  - Anwendung: .NET
  - Datenbank: PostgreSQL
  - Validierungsdienst: KoSIT
  - Dateiablage: Samba
  - Reverse Proxy: Nginx Proxy Manager
- **Netzwerk:** Das System ist nur im lokalen Netz erreichbar (HTTPS). Ausgehende Verbindungen gehen nur zum Mailserver, zu Azure Document Intelligence und zu Webseiten von Lieferanten (Logos).

### 3.2 Datenhaltung und Unveränderbarkeit
- **Originale** liegen im Ablageverzeichnis unter `Ablage/{Jahr}/{Lieferant}/{Datum}_{Rechnungsnr}`. Das System schreibt jede Datei genau einmal.
- **Schreibschutz:** Der Netzwerk-Share „Rechnungen-Ablage“ ist für Benutzer schreibgeschützt und zusätzlich als WORM-Share konfiguriert (Samba `vfs_worm`).
- **Prüfsummen:** Für jede Datei wird beim Ablegen eine SHA-256-Prüfsumme gespeichert. Ein nächtlicher Prüflauf vergleicht alle Dateien mit ihrer Prüfsumme. Abweichungen werden dem Admin gemeldet und protokolliert.
- **Daten:** Ausgelesene Daten und das Änderungsprotokoll liegen in der PostgreSQL-Datenbank. Das Änderungsprotokoll ist technisch gegen Änderung und Löschung gesperrt (Datenbank-Trigger).

### 3.3 Maschinelle Auswertbarkeit / Datenzugriff (§ 147 Abs. 6 AO)
Die Finanzverwaltung erhält Zugriff über einen Nur-Lesen-Benutzer (Z1) oder über den Export der Belegliste und der Originale (Z3).

## 4. Betriebsdokumentation

### 4.1 Datensicherung
- Die Datenbank wird täglich um 02:00 Uhr gesichert (Dump, 30 Tage aufbewahrt) und landet im Ordner `backup`.
- Ablage- und Backup-Ordner sichert **[ERGÄNZEN: Backup-Lösung, Zielgerät/-ort, Rhythmus, Aufbewahrung]**.
- **Wiederherstellungstest:** **[ERGÄNZEN: Rhythmus, z. B. jährlich]**, Ergebnis wird in Anhang A protokolliert.

### 4.2 Aufbewahrung und Löschung
- Aufbewahrungsfrist für Buchungsbelege: 8 Jahre ab Ende des Kalenderjahres (§ 147 AO in der Fassung ab 2025). Das System sperrt Belege bis Fristende gegen Löschung.
- Nach Fristende schlägt das System die Löschung vor. Gelöscht wird nur nach Freigabe durch den Admin; die Löschung wird protokolliert.
- Papieroriginale werden nach denselben Fristen aufbewahrt.

### 4.3 Zugriffsschutz
- Persönliche Benutzerkonten mit Passwort und Zwei-Faktor-Authentifizierung (TOTP) bzw. Google-Konto.
- Neue Konten werden erst nach Freigabe durch den Admin aktiv.
- Rollen: Admin, Benutzer, Nur-Lesen.
- Programmzugriff (MCP): persönliche, widerrufbare Tokens mit eingeschränkten Rechten (lesen, hochladen).

### 4.4 Programmänderungen
Der Quellcode wird in Git versioniert. Jede Version in Produktion ist ein Git-Tag. Änderungen, die Erfassung, Prüfung oder Ablage betreffen, werden in dieser Dokumentation im Änderungsverlauf vermerkt.

## 5. Internes Kontrollsystem (IKS)

| Kontrolle | Häufigkeit | Durchführung |
|---|---|---|
| Inbox und „zu prüfen“ abarbeiten | [ERGÄNZEN, z. B. wöchentlich] | Benutzer |
| Integritätsmeldungen kontrollieren | bei Meldung | Admin |
| Vollständigkeit: Kontoauszüge gegen Belege abgleichen | [ERGÄNZEN, z. B. monatlich] | [ERGÄNZEN] |
| Benutzer und Tokens überprüfen | jährlich | Admin |
| Backup-Wiederherstellung testen | [ERGÄNZEN] | Technischer Betrieb |

## 6. Auslagerung / Dienstleister
| Dienst | Anbieter | Zweck | Datenstandort |
|---|---|---|---|
| Azure Document Intelligence | Microsoft | Auslesen von Belegen | EU-Region [ERGÄNZEN] |
| E-Mail-Postfach | [ERGÄNZEN] | Empfang von Rechnungen | [ERGÄNZEN] |

## 7. Änderungsverlauf dieser Dokumentation
Siehe Tabelle am Anfang. Die Dokumentation liegt im Repository unter `docs/verfahrensdokumentation/` und ist dadurch mitversioniert.

---

### Anhang A – Protokoll der Wiederherstellungstests
| Datum | Getestet | Ergebnis | Durchgeführt von |
|---|---|---|---|
| | | | |
