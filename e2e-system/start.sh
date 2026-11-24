#!/usr/bin/env bash
# Baut und startet das komplette System samt Nachbildungen (azure-mock, mail, werkzeug) mit leeren Volumes.
# Aufruf aus beliebigem Ordner: bash e2e-system/start.sh
set -euo pipefail

hier="$(cd "$(dirname "$0")" && pwd)"

# Reste eines abgebrochenen Laufs entfernen, damit das System leer startet.
bash "$hier/compose.sh" down -v --remove-orphans >/dev/null 2>&1 || true
bash "$hier/compose.sh" up -d --build --wait --wait-timeout 300
