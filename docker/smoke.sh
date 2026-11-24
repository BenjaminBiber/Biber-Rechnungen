#!/usr/bin/env bash
# Smoke-Test: baut und startet alle vier Dienste mit Test-Volumes, prüft API und Frontend,
# räumt am Ende immer auf. Aufruf aus beliebigem Ordner: bash docker/smoke.sh
set -euo pipefail

cd "$(dirname "$0")/.."

cp .env.example .env.test
port="$(grep -E '^API_PORT=' .env.test | cut -d= -f2)"
port="${port:-8080}"

compose=(docker compose --env-file .env.test -f docker-compose.yml -f docker/compose.test.yml -p biber-rechnungen-smoke)

aufraeumen() {
    "${compose[@]}" down -v --remove-orphans || true
    rm -f .env.test
}
trap aufraeumen EXIT

"${compose[@]}" up -d --build --wait --wait-timeout 300

echo "Prüfe /health/ready …"
curl -fsS "http://localhost:${port}/health/ready"
echo

echo "Prüfe / …"
seite="$(curl -fsS "http://localhost:${port}/")"
if ! grep -q '<div id="root">' <<<"$seite"; then
    echo "Fehler: / liefert nicht das gebaute Frontend." >&2
    exit 1
fi

echo "Smoke-Test erfolgreich."
