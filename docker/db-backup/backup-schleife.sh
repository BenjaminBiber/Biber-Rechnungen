#!/bin/sh
# Einstiegspunkt des Containers: wartet bis zur nächsten BACKUP_ZEIT (HH:MM, Zeitzone TZ),
# führt backup.sh aus und wiederholt das. Ein fehlgeschlagener Lauf beendet die Schleife nicht.
# BACKUP_BEIM_START=1: zusätzlich sofort ein Lauf beim Start (für Tests).
set -u

zeit="${BACKUP_ZEIT:-02:00}"
kind=""

if ! echo "$zeit" | grep -Eq '^([01][0-9]|2[0-3]):[0-5][0-9]$'; then
    echo "db-backup: BACKUP_ZEIT muss HH:MM sein, ist aber '$zeit'." >&2
    exit 2
fi

# SIGTERM/SIGINT (docker stop): laufendes Kind beenden und sauber aussteigen.
beenden() {
    echo "db-backup: Signal empfangen, beende."
    if [ -n "$kind" ]; then
        kill "$kind" 2>/dev/null
    fi
    exit 0
}
trap beenden TERM INT

# Sekunden bis zur nächsten BACKUP_ZEIT (immer > 0; ist sie gerade erreicht, dann bis morgen).
sekunden_bis_zeit() {
    h="$(date +%H)"; m="$(date +%M)"; s="$(date +%S)"
    jetzt=$(( ${h#0} * 3600 + ${m#0} * 60 + ${s#0} ))
    zh="${zeit%%:*}"; zm="${zeit##*:}"
    ziel=$(( ${zh#0} * 3600 + ${zm#0} * 60 ))
    rest=$(( ziel - jetzt ))
    if [ "$rest" -le 0 ]; then
        rest=$(( rest + 86400 ))
    fi
    echo "$rest"
}

# Befehl im Hintergrund starten und warten, damit Signale sofort ankommen.
ausfuehren() {
    "$@" &
    kind=$!
    wait "$kind"
    status=$?
    kind=""
    return "$status"
}

sicherung() {
    if ausfuehren /usr/local/bin/backup.sh; then
        echo "db-backup: Lauf erfolgreich."
    else
        echo "db-backup: Lauf fehlgeschlagen (Exit-Code $?); nächster Versuch zur nächsten BACKUP_ZEIT." >&2
    fi
}

echo "db-backup: gestartet, BACKUP_ZEIT=$zeit, BACKUP_TAGE=${BACKUP_TAGE:-30}, TZ=${TZ:-UTC}."

if [ "${BACKUP_BEIM_START:-0}" = "1" ]; then
    sicherung
fi

while :; do
    warten="$(sekunden_bis_zeit)"
    echo "db-backup: Nächster Lauf um $zeit Uhr, in $warten s."
    ausfuehren sleep "$warten"
    sicherung
done
