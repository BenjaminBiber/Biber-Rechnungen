#!/usr/bin/env bash
# Berechnet die nächste SemVer-Version aus dem letzten Tag und der Stufe.
#   bash naechste-version.sh <letzter-tag|leer> <patch|minor|major>
# Ausgabe: X.Y.Z (ohne führendes v). Ohne Tag gilt 0.0.0 als Ausgangspunkt.
set -euo pipefail

letzter="${1:-}"
stufe="${2:-}"

letzter="${letzter#v}"
[ -z "$letzter" ] && letzter="0.0.0"

if ! [[ "$letzter" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
  echo "Ungültiger Tag: '$1' (erwartet vX.Y.Z)" >&2
  exit 1
fi
major="${BASH_REMATCH[1]}"
minor="${BASH_REMATCH[2]}"
patch="${BASH_REMATCH[3]}"

case "$stufe" in
  major) major=$((major + 1)); minor=0; patch=0 ;;
  minor) minor=$((minor + 1)); patch=0 ;;
  patch) patch=$((patch + 1)) ;;
  *) echo "Ungültige Stufe: '$stufe' (erwartet patch, minor oder major)" >&2; exit 1 ;;
esac

echo "${major}.${minor}.${patch}"
