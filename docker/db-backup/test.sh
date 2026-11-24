#!/usr/bin/env bash
# Test des db-backup-Images: Dump, Wiederherstellung, Rotation, Fehlerfall, Schleife (Start-Lauf, SIGTERM, Healthcheck).
# Startet Postgres 17 und das Image in einem eigenen Docker-Netz mit temporärem Volume und räumt immer auf.
# Aufruf aus beliebigem Ordner: bash docker/db-backup/test.sh
set -euo pipefail

# Git Bash unter Windows würde Pfade wie /backup in Docker-Argumenten umschreiben.
export MSYS_NO_PATHCONV=1

cd "$(dirname "$0")"

lauf="$$"
image="biber-db-backup-test:$lauf"
netz="biber-db-backup-test-$lauf"
volumen="biber-db-backup-test-$lauf"
datenbank="rechnungen"
passwort="test-passwort"
postgres="biber-db-backup-test-pg-$lauf"
schleife="biber-db-backup-test-loop-$lauf"

aufraeumen() {
    docker rm -f "$postgres" "$schleife" > /dev/null 2>&1 || true
    docker network rm "$netz" > /dev/null 2>&1 || true
    docker volume rm "$volumen" > /dev/null 2>&1 || true
    docker image rm "$image" > /dev/null 2>&1 || true
}
trap aufraeumen EXIT

pruefungen=0
pruefen() {
    # pruefen "<Beschreibung>" <Befehl …>: bricht ab, wenn der Befehl fehlschlägt.
    local text="$1"
    shift
    if "$@"; then
        pruefungen=$((pruefungen + 1))
        echo "  ok: $text"
    else
        echo "FEHLER: $text" >&2
        exit 1
    fi
}

# Befehl im Image-Container mit Datenbankanbindung und Backup-Volume ausführen.
im_image() {
    docker run --rm --network "$netz" \
        -e PGHOST="$postgres" -e PGUSER=rechnungen -e PGPASSWORD="$passwort" -e PGDATABASE="$datenbank" \
        -e BACKUP_TAGE=30 \
        -v "$volumen:/backup" "$@"
}
backup() { im_image --entrypoint /usr/local/bin/backup.sh "$image"; }
shell() { im_image --entrypoint sh "$image" -c "$1"; }

# Datei zurückdatieren: touch -t mit berechnetem Zeitstempel (BusyBox kennt nicht alle touch -d-Formate).
zurueckdatieren() {
    shell "t=\$(date -u -d @\$(( \$(date +%s) - $2 * 86400 )) +%Y%m%d%H%M.%S) && touch -t \"\$t\" '/backup/$1'"
}

anzahl() {
    # anzahl "<Muster>": Anzahl der Dateien im Backup-Volumen.
    shell "ls /backup | grep -c '$1' || true"
}

echo "Baue Image …"
docker build -q -t "$image" . > /dev/null
docker network create "$netz" > /dev/null
docker volume create "$volumen" > /dev/null

echo "Starte Postgres 17 …"
docker run -d --name "$postgres" --network "$netz" \
    -e POSTGRES_USER=rechnungen -e POSTGRES_PASSWORD="$passwort" -e POSTGRES_DB="$datenbank" \
    postgres:17 > /dev/null
for _ in $(seq 1 60); do
    if docker exec "$postgres" pg_isready -U rechnungen -d "$datenbank" -h 127.0.0.1 > /dev/null 2>&1; then
        break
    fi
    sleep 1
done
# Der Entrypoint startet erst einen temporären Server; sicherheitshalber kurz nach dem ersten Erfolg erneut prüfen.
sleep 3
docker exec "$postgres" pg_isready -U rechnungen -d "$datenbank" -h 127.0.0.1 > /dev/null

psql_ausfuehren() {
    docker exec -e PGPASSWORD="$passwort" "$postgres" psql -U rechnungen -h 127.0.0.1 -v ON_ERROR_STOP=1 -tA "$@"
}
psql_ausfuehren -d "$datenbank" -c "create table belege (id int primary key, titel text not null);" \
    -c "insert into belege values (1, 'Büromaterial'), (2, 'Strom'), (3, 'Miete');" > /dev/null

echo "Dump erzeugen …"
backup
pruefen "genau ein *.dump entsteht" test "$(anzahl '^biber-rechnungen_.*\.dump$')" = 1
pruefen "keine *.tmp bleibt liegen" test "$(anzahl '\.tmp$')" = 0

echo "Wiederherstellung prüfen …"
psql_ausfuehren -d postgres -c "create database wiederhergestellt;" > /dev/null
shell "pg_restore --dbname=wiederhergestellt --exit-on-error /backup/biber-rechnungen_*.dump"
original="$(psql_ausfuehren -d "$datenbank" -c "select id, titel from belege order by id;")"
kopie="$(psql_ausfuehren -d wiederhergestellt -c "select id, titel from belege order by id;")"
pruefen "wiederhergestellte Datenbank liefert dieselben Daten" test -n "$original" -a "$original" = "$kopie"

echo "Rotation prüfen …"
dump="$(shell "ls /backup | grep '^biber-rechnungen_.*\.dump$'")"
shell "cp '/backup/$dump' /backup/biber-rechnungen_alt31.dump && cp '/backup/$dump' /backup/biber-rechnungen_alt29.dump \
    && echo x > /backup/biber-rechnungen_unfertig.dump.tmp && echo x > /backup/notiz.txt"
zurueckdatieren biber-rechnungen_alt31.dump 31
zurueckdatieren biber-rechnungen_alt29.dump 29
zurueckdatieren notiz.txt 400
sleep 1 # der nächste Dump bekommt einen neuen Zeitstempel im Dateinamen
backup
pruefen "31 Tage alter Dump wird gelöscht" test "$(anzahl '^biber-rechnungen_alt31\.dump$')" = 0
pruefen "29 Tage alter Dump bleibt" test "$(anzahl '^biber-rechnungen_alt29\.dump$')" = 1
pruefen "übrig gebliebene *.tmp wird entfernt" test "$(anzahl '\.tmp$')" = 0
pruefen "fremde Dateien bleiben unberührt" test "$(anzahl '^notiz\.txt$')" = 1
pruefen "beide echten Dumps bleiben" test "$(anzahl '^biber-rechnungen_2.*\.dump$')" = 2

echo "Fehlerfall prüfen …"
vorher="$(shell "cd /backup && stat -c '%n %s %Y' *")"
pruefen "Ausgangszustand für den Vergleich ist nicht leer" test -n "$vorher"
fehlgeschlagen=0
docker run --rm --network "$netz" \
    -e PGHOST="$postgres" -e PGUSER=rechnungen -e PGPASSWORD=falsch -e PGDATABASE="$datenbank" -e BACKUP_TAGE=0 \
    -v "$volumen:/backup" --entrypoint /usr/local/bin/backup.sh "$image" 2> /tmp/db-backup-test-stderr-$lauf.txt || fehlgeschlagen=$?
pruefen "Lauf mit falschem Passwort endet mit Exit-Code != 0" test "$fehlgeschlagen" -ne 0
pruefen "Fehlermeldung steht auf stderr" grep -q 'FEHLER' "/tmp/db-backup-test-stderr-$lauf.txt"
rm -f "/tmp/db-backup-test-stderr-$lauf.txt"
nachher="$(shell "cd /backup && stat -c '%n %s %Y' *")"
pruefen "vorhandene Dumps bleiben bei fehlgeschlagenem Lauf stehen (auch mit BACKUP_TAGE=0)" test "$vorher" = "$nachher"

echo "Schleife prüfen …"
docker run -d --name "$schleife" --network "$netz" \
    -e PGHOST="$postgres" -e PGUSER=rechnungen -e PGPASSWORD="$passwort" -e PGDATABASE="$datenbank" \
    -e BACKUP_ZEIT=03:30 -e BACKUP_BEIM_START=1 \
    -v "$volumen:/backup" "$image" > /dev/null
for _ in $(seq 1 30); do
    if docker logs "$schleife" 2>&1 | grep -q 'Nächster Lauf um 03:30 Uhr'; then
        break
    fi
    sleep 1
done
pruefen "Start-Lauf wurde ausgeführt" bash -c "docker logs '$schleife' 2>&1 | grep -q 'Lauf erfolgreich'"
pruefen "Schleife nennt die Wartezeit bis zur nächsten BACKUP_ZEIT" bash -c "docker logs '$schleife' 2>&1 | grep -Eq 'Nächster Lauf um 03:30 Uhr, in [0-9]+ s'"
pruefen "Healthcheck ist gesund" docker exec "$schleife" /usr/local/bin/healthcheck.sh
zurueckdatieren_alle() {
    shell "t=\$(date -u -d @\$(( \$(date +%s) - 2 * 86400 )) +%Y%m%d%H%M.%S) && touch -t \"\$t\" /backup/biber-rechnungen_*.dump"
}
zurueckdatieren_alle
pruefen "Healthcheck ist ungesund, wenn der jüngste Dump älter als 26 Stunden ist" \
    bash -c "! docker exec '$schleife' /usr/local/bin/healthcheck.sh 2> /dev/null"
start="$(date +%s)"
docker stop -t 20 "$schleife" > /dev/null
dauer=$(( $(date +%s) - start ))
pruefen "SIGTERM beendet die Schleife zügig (${dauer} s)" test "$dauer" -lt 10
pruefen "Schleife endet mit Exit-Code 0" test "$(docker inspect -f '{{.State.ExitCode}}' "$schleife")" = 0

echo "db-backup-Test erfolgreich ($pruefungen Prüfungen)."
