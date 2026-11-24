#!/usr/bin/env bash
# Stoppt das E2E-System und entfernt alle Volumes.
# Aufruf aus beliebigem Ordner: bash e2e-system/stop.sh
set -euo pipefail

bash "$(dirname "$0")/compose.sh" down -v --remove-orphans
