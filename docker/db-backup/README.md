# db-backup

Container für die nächtliche Datenbanksicherung. Basis `postgres:17-alpine` (passende `pg_dump`-Version).

## Ablauf

- `backup-schleife.sh` (Einstiegspunkt) wartet bis zur nächsten `BACKUP_ZEIT` und ruft `backup.sh` auf. Ein fehlgeschlagener Lauf beendet die Schleife nicht; `docker stop` (SIGTERM) beendet sie sauber.
- `backup.sh` schreibt `pg_dump --format=custom` nach `/backup/biber-rechnungen_<yyyyMMdd-HHmmss>.dump.tmp`, prüft ihn mit `pg_restore --list` und benennt ihn erst danach in `….dump` um. Unfertige Dateien sind so nie als gültiger Dump sichtbar.
- Danach Rotation: `*.dump` älter als `BACKUP_TAGE` Tage werden gelöscht, übrig gebliebene `*.dump.tmp` ebenfalls. Schlägt der Dump fehl, endet der Lauf mit Exit-Code ≠ 0 und einer Meldung auf stderr; vorhandene Dumps bleiben unangetastet.
- `HEALTHCHECK` (`healthcheck.sh`): gesund, solange die Schleife läuft und – falls schon ein Dump existiert – der jüngste nicht älter als 26 Stunden ist.

## Umgebung

| Variable | Bedeutung | Standard |
| --- | --- | --- |
| `PGHOST`, `PGUSER`, `PGPASSWORD`, `PGDATABASE` | Verbindung zur Datenbank | aus `docker-compose.yml` |
| `BACKUP_ZEIT` | Uhrzeit des Laufs, `HH:MM` | `02:00` |
| `BACKUP_TAGE` | Aufbewahrung in Tagen | `30` |
| `BACKUP_BEIM_START` | `1` = zusätzlich sofort ein Lauf beim Start (Tests) | aus |
| `TZ` | Zeitzone für `BACKUP_ZEIT` | `Europe/Berlin` |

Mount `${PFAD_BACKUP}:/backup`.

## Wiederherstellung

Dump aus `${PFAD_BACKUP}` in eine (leere oder bestehende) Datenbank einspielen; `--clean --if-exists` ersetzt vorhandene Objekte:

```bash
docker compose exec -T db-backup sh -c \
  'pg_restore --clean --if-exists --no-owner -d "$PGDATABASE" /backup/biber-rechnungen_<yyyyMMdd-HHmmss>.dump'
```

Vorher die API stoppen (`docker compose stop api`), danach wieder starten. Inhalt eines Dumps ansehen: `pg_restore --list <datei>`.

## Host-Backup

`${PFAD_BACKUP}` liegt auf dem Host und wird vom vorhandenen Host-Backup mitgesichert (P3): Die Dumps liegen damit nicht nur auf demselben Datenträger wie die Datenbank.

## Test

`bash docker/db-backup/test.sh` (Docker erforderlich): startet Postgres 17 und das Image in einem eigenen Netz mit temporärem Volume und prüft Dump, Wiederherstellung, Rotation (31 Tage gelöscht, 29 Tage bleibt), Fehlerfall (falsches Passwort lässt alte Dumps stehen) sowie Schleife, SIGTERM und Healthcheck. Räumt immer auf.
