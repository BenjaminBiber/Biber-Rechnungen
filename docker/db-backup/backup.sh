#!/bin/sh
# Einmaliger Sicherungslauf: pg_dump (custom-Format) nach BACKUP_VERZEICHNIS, prüfen, umbenennen, rotieren.
# Verbindung über die üblichen PG*-Variablen (PGHOST, PGUSER, PGPASSWORD, PGDATABASE).
# Schlägt der Dump fehl, endet das Skript mit Exit-Code != 0 und rührt vorhandene Dumps nicht an.
set -eu

verzeichnis="${BACKUP_VERZEICHNIS:-/backup}"
tage="${BACKUP_TAGE:-30}"
praefix="biber-rechnungen"

fehler() {
    echo "db-backup: FEHLER: $*" >&2
}

case "$tage" in
    ''|*[!0-9]*)
        fehler "BACKUP_TAGE muss eine ganze Zahl sein, ist aber '$tage'."
        exit 2
        ;;
esac

if [ -z "${PGDATABASE:-}" ]; then
    fehler "PGDATABASE ist nicht gesetzt."
    exit 2
fi

if [ ! -d "$verzeichnis" ] || [ ! -w "$verzeichnis" ]; then
    fehler "Verzeichnis '$verzeichnis' fehlt oder ist nicht beschreibbar."
    exit 1
fi

zeitstempel="$(date +%Y%m%d-%H%M%S)"
ziel="$verzeichnis/${praefix}_${zeitstempel}.dump"
temporaer="$ziel.tmp"

# Unfertige Datei bei Fehlern und bei SIGTERM nicht liegen lassen.
trap 'rm -f "$temporaer"' EXIT
trap 'exit 143' TERM INT

echo "db-backup: sichere Datenbank '$PGDATABASE' nach $ziel"

if ! pg_dump --format=custom --file="$temporaer"; then
    fehler "pg_dump ist fehlgeschlagen; vorhandene Dumps bleiben unverändert."
    exit 1
fi

if ! pg_restore --list "$temporaer" > /dev/null; then
    fehler "Der Dump ist nicht lesbar (pg_restore --list); vorhandene Dumps bleiben unverändert."
    exit 1
fi

mv "$temporaer" "$ziel"
echo "db-backup: Dump fertig ($(wc -c < "$ziel") Bytes)"

# Rotation erst nach erfolgreichem Dump: Dumps älter als BACKUP_TAGE Tage und übrig gebliebene *.tmp löschen.
minuten=$((tage * 1440))
find "$verzeichnis" -maxdepth 1 -type f -name "${praefix}_*.dump" -mmin "+$minuten" -print -delete |
    while IFS= read -r datei; do
        echo "db-backup: alter Dump gelöscht: $datei"
    done
find "$verzeichnis" -maxdepth 1 -type f -name "${praefix}_*.dump.tmp" ! -path "$temporaer" -delete
