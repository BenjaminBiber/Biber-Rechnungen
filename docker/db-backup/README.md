# db-backup

Lauffähiger Platzhalter: kleines Image, das nur schläft.
Wird vom Ticket für die Datenbanksicherung ersetzt; dabei ändert sich nur dieser Ordner, nicht `docker-compose.yml`.

Vorhandene Umgebung aus `docker-compose.yml`: `PGHOST=postgres`, `PGUSER`, `PGPASSWORD`, `PGDATABASE`, `BACKUP_ZEIT=02:00`, `BACKUP_TAGE=30`, Mount `${PFAD_BACKUP}:/backup`.
