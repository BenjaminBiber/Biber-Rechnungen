#!/bin/sh
# Gesund, solange die Schleife als Hauptprozess läuft und – falls schon ein Dump existiert –
# der jüngste höchstens 26 Stunden alt ist. Noch kein Dump: gesund (erste Sicherung steht aus).
set -u

verzeichnis="${BACKUP_VERZEICHNIS:-/backup}"

if ! tr '\0' ' ' < /proc/1/cmdline | grep -q 'backup-schleife'; then
    echo "Schleife läuft nicht." >&2
    exit 1
fi

if [ -z "$(find "$verzeichnis" -maxdepth 1 -type f -name 'biber-rechnungen_*.dump' 2>/dev/null | head -n 1)" ]; then
    exit 0
fi

if [ -z "$(find "$verzeichnis" -maxdepth 1 -type f -name 'biber-rechnungen_*.dump' -mmin -1560 2>/dev/null | head -n 1)" ]; then
    echo "Der jüngste Dump ist älter als 26 Stunden." >&2
    exit 1
fi
