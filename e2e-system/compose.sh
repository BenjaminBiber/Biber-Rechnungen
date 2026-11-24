#!/usr/bin/env bash
# Gemeinsamer Aufruf von Docker Compose für das E2E-System (von start.sh, stop.sh und den Tests genutzt).
# Aufruf: bash e2e-system/compose.sh <compose-Befehl …>
set -euo pipefail

cd "$(dirname "$0")/.."

exec docker compose \
    --env-file e2e-system/e2e.env \
    -f docker-compose.yml \
    -f docker/compose.test.yml \
    -f e2e-system/compose.e2e.yml \
    -p biber-e2e \
    "$@"
