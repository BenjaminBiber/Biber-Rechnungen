#!/usr/bin/env bash
# Kompletter Lauf: Abhängigkeiten → System starten → Playwright-Tests → System stoppen.
# Exit-Code ist der der Tests. Aufruf aus beliebigem Ordner: bash e2e-system/lauf.sh [Playwright-Argumente …]
# Mit E2E_LOGS=<Datei> landen die Container-Logs vor dem Stoppen in dieser Datei (CI).
set -uo pipefail

hier="$(cd "$(dirname "$0")" && pwd)"
cd "$hier"

aufraeumen() {
    if [[ -n "${E2E_LOGS:-}" ]]; then
        bash "$hier/compose.sh" logs --no-color >"$E2E_LOGS" 2>&1 || true
    fi
    bash "$hier/stop.sh" || true
}

npm ci --no-audit --no-fund || exit 1
npx playwright install chromium || exit 1

trap aufraeumen EXIT
bash "$hier/start.sh" || exit 1

npx playwright test "$@"
