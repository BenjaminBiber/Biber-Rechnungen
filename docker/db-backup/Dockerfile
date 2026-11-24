# Nächtliche Datenbanksicherung: pg_dump im Container, Rotation nach BACKUP_TAGE Tagen.
# Basis liefert pg_dump/pg_restore in der Version des Datenbankdienstes (PostgreSQL 17).
FROM postgres:17-alpine

RUN apk add --no-cache tzdata

ENV TZ=Europe/Berlin \
    BACKUP_ZEIT=02:00 \
    BACKUP_TAGE=30

COPY --chmod=755 backup.sh backup-schleife.sh healthcheck.sh /usr/local/bin/

# Überschreibt den Einstiegspunkt der Postgres-Basis (der würde einen Datenbankserver starten).
ENTRYPOINT ["/usr/local/bin/backup-schleife.sh"]
CMD []

# Gesund: Schleife läuft und der jüngste Dump ist höchstens 26 Stunden alt (noch kein Dump: gesund).
# Während der Startphase wird oft geprüft, damit `docker compose --wait` nicht lange wartet.
HEALTHCHECK --interval=5m --timeout=10s --start-period=30s --start-interval=3s --retries=1 \
    CMD ["/usr/local/bin/healthcheck.sh"]
