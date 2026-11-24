#!/usr/bin/env bash
# Tests für naechste-version.sh: bash .github/scripts/naechste-version.test.sh
set -uo pipefail

skript="$(dirname "$0")/naechste-version.sh"
fehler=0

pruefe() {
  local erwartet="$1"; shift
  local ist
  ist="$(bash "$skript" "$@" 2>/dev/null)"
  if [ "$ist" = "$erwartet" ]; then
    echo "ok    $* -> $ist"
  else
    echo "FEHLER $* -> '$ist' (erwartet '$erwartet')"
    fehler=1
  fi
}

pruefe_fehler() {
  if bash "$skript" "$@" >/dev/null 2>&1; then
    echo "FEHLER $* sollte scheitern"
    fehler=1
  else
    echo "ok    $* scheitert"
  fi
}

pruefe 0.0.1 "" patch
pruefe 0.1.0 "" minor
pruefe 1.0.0 "" major
pruefe 1.2.4 v1.2.3 patch
pruefe 1.3.0 v1.2.3 minor
pruefe 2.0.0 v1.2.3 major
pruefe 1.2.10 v1.2.9 patch
pruefe_fehler v1.2.3 gross
pruefe_fehler v1.2 patch
pruefe_fehler kaputt patch

exit "$fehler"
