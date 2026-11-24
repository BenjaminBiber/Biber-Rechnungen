# Betrieb von Biber-Rechnungen

Diese Anleitung beschreibt Einrichtung, Betrieb, Sicherung und Wiederherstellung. Architektur und Hintergründe stehen im [Plan](plan.md), die GoBD-Sicht in der [Verfahrensdokumentation](verfahrensdokumentation/verfahrensdokumentation.md).

## Voraussetzungen

- Docker-Host (Linux empfohlen; Docker Desktop unter Windows funktioniert ebenfalls, der Ordner-Watcher sichert sich per Polling ab) mit Docker Compose v2.
- Ein **vorhandener Samba-Container** auf demselben Host. Biber-Rechnungen startet keinen eigenen Samba.
- Ein **vorhandener Nginx Proxy Manager** auf demselben Host für HTTPS.
- Ein Host-Backup, das Ordner auf **ein anderes Gerät** sichert.

Biber-Rechnungen besteht aus vier Diensten in `docker-compose.yml`:

| Dienst | Zweck |
| --- | --- |
| `api` | Weboberfläche, REST-API, MCP-Endpunkt `/mcp`, Hintergrundjobs (Hangfire), Ordner-Watcher, IMAP-Abruf |
| `postgres` | Datenbank (PostgreSQL 17), Volume `pgdata` |
| `kosit` | Validierung und Visualisierung von E-Rechnungen, nur im internen Netz erreichbar |
| `db-backup` | nächtlicher `pg_dump` nach `PFAD_BACKUP`, 30 Tage rotierend |

## Verzeichnisse anlegen und Rechte setzen

Alle fünf Pfade aus `.env` müssen existieren und dem Container-Benutzer `APP_UID:APP_GID` gehören:

| Variable | Standard | Inhalt |
| --- | --- | --- |
| `PFAD_EINGANG` | `/srv/rechnungen/eingang` | Eingangsordner, Samba-Freigabe `Rechnungen-Eingang` |
| `PFAD_STAGING` | `/srv/rechnungen/staging` | Zwischenablage während der Verarbeitung |
| `PFAD_ABLAGE` | `/srv/rechnungen/ablage` | unveränderbare Belegablage, Samba-Freigabe `Rechnungen-Ablage` |
| `PFAD_DATEN` | `/srv/rechnungen/daten` | Anwendungsdaten (ML-Modelle, Schlüssel) |
| `PFAD_BACKUP` | `/srv/rechnungen/backup` | Datenbanksicherungen (`db-backup` schreibt, `api` liest nur) |

```bash
sudo mkdir -p /srv/rechnungen/{eingang,staging,ablage,daten,backup}
sudo chown -R 1000:1000 /srv/rechnungen   # APP_UID:APP_GID
sudo chmod 770 /srv/rechnungen/*
```

## `.env` ausfüllen

```bash
cp .env.example .env
```

| Variable | Bedeutung | Beispiel | Leer erlaubt |
| --- | --- | --- | --- |
| `POSTGRES_USER` | Datenbankbenutzer | `rechnungen` | nein |
| `POSTGRES_PASSWORD` | Datenbankpasswort, **unbedingt ändern** | langes Zufallspasswort | nein |
| `POSTGRES_DB` | Datenbankname | `rechnungen` | nein |
| `PFAD_EINGANG` | Host-Pfad des Eingangsordners | `/srv/rechnungen/eingang` | nein |
| `PFAD_STAGING` | Host-Pfad der Zwischenablage | `/srv/rechnungen/staging` | nein |
| `PFAD_ABLAGE` | Host-Pfad der Belegablage | `/srv/rechnungen/ablage` | nein |
| `PFAD_DATEN` | Host-Pfad der Anwendungsdaten | `/srv/rechnungen/daten` | nein |
| `PFAD_BACKUP` | Host-Pfad der Datenbanksicherungen | `/srv/rechnungen/backup` | nein |
| `APP_UID` | Benutzer-ID des API-Containers, Besitzer der Verzeichnisse | `1000` | ja (Standard 1000) |
| `APP_GID` | Gruppen-ID des API-Containers | `1000` | ja (Standard 1000) |
| `API_PORT` | Port auf dem Host | `8080` | ja (Standard 8080) |
| `OEFFENTLICHE_URL` | Adresse im LAN, für Links und Google-Redirect | `https://rechnungen.example.lan` | ja, aber für Google-Anmeldung nötig |
| `AZURE_DI_ENDPUNKT` | Endpunkt von Azure Document Intelligence | `https://<name>.cognitiveservices.azure.com/` | ja (Auslesen von PDFs ohne E-Rechnung aus) |
| `AZURE_DI_SCHLUESSEL` | Schlüssel dazu | `…` | ja |
| `IMAP_HOST` | IMAP-Server des Rechnungspostfachs | `imap.ionos.de` | ja (Mail-Import aus) |
| `IMAP_PORT` | IMAP-Port (TLS) | `993` | ja (Standard 993) |
| `IMAP_BENUTZER` | Anmeldename des Postfachs | `rechnungen@example.de` | ja |
| `IMAP_PASSWORT` | App-Passwort des Postfachs | `…` | ja |
| `GOOGLE_CLIENT_ID` | OAuth-Client-ID für die Google-Anmeldung | `…apps.googleusercontent.com` | ja (Google-Anmeldung aus) |
| `GOOGLE_CLIENT_SECRET` | OAuth-Client-Secret | `…` | ja |

Fachliche Einstellungen (Schwellen, Limits, USt-Status, Aufbewahrung) stehen nicht in `.env`, sondern auf der Einstellungsseite.

## Start und Ersteinrichtung

```bash
docker compose up -d
docker compose ps        # alle vier Dienste "running", api und postgres "healthy"
```

Die Datenbankmigration läuft beim Start der `api` automatisch. Danach im Browser `https://rechnungen.<domain>` (oder `http://<host>:<API_PORT>`) öffnen: Die Ersteinrichtung legt das erste Konto an, das automatisch Admin wird. Anschließend Zwei-Faktor-Anmeldung (TOTP) einrichten.

## Samba-Freigaben

Die Beispielkonfiguration [`docker/samba/smb.conf.example`](../docker/samba/smb.conf.example) enthält die Freigaben `Rechnungen-Eingang` (lesen und schreiben) und `Rechnungen-Ablage` (nur lesen, `vfs objects = worm`). Die Einbindung in den vorhandenen Samba-Container beschreibt [`docker/samba/README.md`](../docker/samba/README.md).

> **Hinweis (P2):** Die App schreibt über den Bind-Mount, nicht über SMB. Das WORM-Modul schützt deshalb den Zugriff über die Freigabe. Gegen Änderungen direkt auf dem Host schützen die SHA-256-Prüfsumme jeder Datei und die nächtliche Integritätsprüfung, die bei Abweichungen eine Benachrichtigung erzeugt.

## Nginx Proxy Manager

1. **Proxy-Host** anlegen: Domain `rechnungen.<domain>`, Ziel `http://api:8080` (gemeinsames Docker-Netz) bzw. `http://<host-ip>:<API_PORT>`.
2. **WebSocket-Unterstützung** einschalten.
3. **SSL:** Let's-Encrypt-Zertifikat per DNS-Challenge mit IONOS oder ein vorhandenes Wildcard-Zertifikat wählen; „Force SSL“ und HTTP/2 an.
4. **Access-List** „nur LAN“ (z. B. `allow 192.168.0.0/16; deny all`) zuweisen.
5. Unter „Advanced“ für Uploads eintragen:

   ```nginx
   client_max_body_size 60m;
   ```

6. **DNS:** `rechnungen.<domain>` zeigt auf die LAN-IP des Docker-Hosts (lokaler DNS oder Eintrag beim DNS-Anbieter).

## Scanner einrichten

Am Scanner ein Scan-to-SMB-Ziel anlegen: `\\<host>\Rechnungen-Eingang`, Konto aus der Samba-Gruppe `rechnungen`, Format PDF. Die App übernimmt eine Datei, sobald ihre Größe 10 Sekunden stabil ist (Absicherung per Polling alle 30 s). Akzeptiert werden PDF, XML, PNG, JPG und TIFF.

## Postfach

- IMAP mit TLS (Port 993) und einem **App-Passwort**, nicht dem Hauptpasswort.
- Das Postfach wird alle 5 Minuten abgerufen; verarbeitet werden Anhänge (PDF, XML, Bilder).
- Verarbeitete Mails landen im IMAP-Ordner `Verarbeitet`, Mails ohne verwertbaren Anhang in `Ohne Beleg`. Die Ordner werden bei Bedarf angelegt; nichts wird gelöscht.

## Azure Document Intelligence

1. Im Azure-Portal eine Ressource „Document Intelligence“ in einer **EU-Region** (z. B. West Europe, Germany West Central) anlegen.
2. Unter „Schlüssel und Endpunkt“ Endpunkt und Schlüssel 1 nach `AZURE_DI_ENDPUNKT` und `AZURE_DI_SCHLUESSEL` übernehmen.
3. Das Seitenlimit pro Monat auf der Einstellungsseite setzen (Warnung bei 80 %, bei 100 % pausiert die Verarbeitung).

## Google-Anmeldung

1. In der Google Cloud Console einen OAuth-Client vom Typ „Webanwendung“ anlegen.
2. Autorisierte Redirect-URL: `https://rechnungen.<domain>/api/auth/google/signin` (Google muss den Server dafür nicht erreichen).
3. Client-ID und Secret nach `GOOGLE_CLIENT_ID` und `GOOGLE_CLIENT_SECRET` übernehmen, `OEFFENTLICHE_URL` setzen, `docker compose up -d`.

Neue Konten (auch über Google) sind gesperrt, bis ein Admin sie freigibt.

## MCP verbinden

In der Weboberfläche unter Einstellungen → API-Tokens ein Token mit den Scopes `lesen` und ggf. `upload` erzeugen, dann:

```bash
claude mcp add --transport http biber-rechnungen https://rechnungen.<domain>/mcp \
  --header "Authorization: Bearer <API-Token>"
```

## Backup

- Der Dienst `db-backup` schreibt jede Nacht um 02:00 Uhr einen `pg_dump` nach `PFAD_BACKUP` (`biber-rechnungen_<yyyyMMdd-HHmmss>.dump`) und löscht Dumps, die älter als 30 Tage sind. Details: [`docker/db-backup/README.md`](../docker/db-backup/README.md).
- **Der Backup-Ordner (`PFAD_BACKUP`) und die Ablage (`PFAD_ABLAGE`) müssen vom vorhandenen Host-Backup auf ein anderes Gerät gesichert werden (P3).** Ohne diese Sicherung ist die Aufbewahrung bei einem Hardwaredefekt nicht gewährleistet. Empfohlen zusätzlich: `PFAD_DATEN` (ML-Modelle, Schlüssel).
- Der Schlüsselbund für Anmelde-Cookies und Links (Passwort zurücksetzen, Bestätigungen) liegt unter `PFAD_DATEN/schluessel` und gehört zur Sicherung von `PFAD_DATEN`. Er übersteht `docker compose up -d --build`. Fehlt er (z. B. nach dem Löschen von `PFAD_DATEN`), legt die Anwendung beim Start einen neuen an: Alle müssen sich neu anmelden und offene Links verfallen, Daten gehen dabei nicht verloren. Das Log warnt „No XML encryptor configured“; das ist erwartet, der Ordner ist durch die Dateirechte (`APP_UID`) geschützt.
- Das Volume `pgdata` muss nicht gesichert werden; die Dumps genügen.

## Wiederherstellung

1. Anwendung anhalten: `docker compose stop api`.
2. Ablage aus dem Host-Backup nach `PFAD_ABLAGE` zurückspielen (Besitzer `APP_UID:APP_GID`), ebenso `PFAD_DATEN` und `PFAD_BACKUP`.
3. Datenbank starten und den gewünschten Dump einspielen:

   ```bash
   docker compose up -d postgres db-backup
   docker compose exec -T db-backup sh -c \
     'pg_restore --clean --if-exists --no-owner -d "$PGDATABASE" /backup/biber-rechnungen_<yyyyMMdd-HHmmss>.dump'
   ```

4. Anwendung starten: `docker compose up -d`.
5. Als Admin unter Einstellungen → Integritätsstatus die **Integritätsprüfung starten** und kontrollieren, dass alle Dateien zu ihren Prüfsummen passen.
6. Den Test im Anhang A der Verfahrensdokumentation protokollieren.

## Aktualisieren

```bash
git pull
docker compose up -d --build
```

Die Datenbankmigration läuft beim Start automatisch. Vorher prüfen, dass der letzte nächtliche Dump in `PFAD_BACKUP` vorhanden ist. Produktive Versionen sind Git-Tags; Änderungen an Erfassung, Prüfung oder Ablage werden in der Verfahrensdokumentation vermerkt.

## Fehlersuche

- **Logs:** `docker compose logs -f api` (ebenso `postgres`, `kosit`, `db-backup`).
- **Healthchecks:** `docker compose ps`; die API meldet `/health/live` (Prozess läuft) und `/health/ready` (Datenbank erreichbar). `db-backup` ist ungesund, wenn der jüngste Dump älter als 26 Stunden ist.
- **Häufige Ursachen:**
  - Dateien im Eingang werden nicht übernommen → Rechte: Besitzer muss `APP_UID:APP_GID` sein (`force user`/`force group` in der Freigabe prüfen).
  - `api` startet nicht → `postgres` nicht gesund oder `POSTGRES_PASSWORD` nach dem ersten Start geändert.
  - Upload bricht ab → `client_max_body_size 60m` im Proxy-Host fehlt.
  - Nach einem Neustart sind alle abgemeldet → `PFAD_DATEN/schluessel` fehlt oder ist für `APP_UID` nicht beschreibbar.
  - Google-Anmeldung meldet „redirect_uri_mismatch“ → Redirect-URL und `OEFFENTLICHE_URL` stimmen nicht überein.
  - PDFs werden nicht ausgelesen → `AZURE_DI_ENDPUNKT`/`AZURE_DI_SCHLUESSEL` leer oder Seitenlimit erreicht (Glocke).
  - Keine Mails → IMAP-Zugang falsch oder App-Passwort abgelaufen; Logs der `api` zeigen den Fehler.
