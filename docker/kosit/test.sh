#!/usr/bin/env bash
# Baut das KoSIT-Sidecar-Image, startet es auf einem freien Port und prüft die HTTP-Schnittstelle.
# Exit-Code 0 nur, wenn alle Prüfungen bestehen. Der Container wird immer abgeräumt.
set -euo pipefail

VERZEICHNIS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TESTDATEN="$VERZEICHNIS/test"
IMAGE="biber-kosit:test"
CONTAINER="biber-kosit-test-$$"
ARBEIT="$(mktemp -d)"
FEHLER=0

aufraeumen() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  rm -rf "$ARBEIT"
}
trap aufraeumen EXIT

ok()     { echo "  OK    $1"; }
fehler() { echo "  FEHLER $1"; FEHLER=$((FEHLER + 1)); }

echo "== Image bauen"
docker build -t "$IMAGE" "$VERZEICHNIS"

echo "== Container starten"
docker run -d --name "$CONTAINER" -p 127.0.0.1::8080 "$IMAGE" >/dev/null
PORT=""
for _ in $(seq 1 30); do
  PORT="$(docker port "$CONTAINER" 8080/tcp 2>/dev/null | head -n1 | sed 's/.*://' || true)"
  [ -n "$PORT" ] && break
  sleep 1
done
if [ -z "$PORT" ]; then
  docker logs "$CONTAINER" || true
  echo "Kein Port für den Container ermittelt"; exit 1
fi
BASIS="http://127.0.0.1:$PORT"
echo "   $BASIS"

echo "== Warten auf /health"
for _ in $(seq 1 120); do
  if [ "$(curl -s -o /dev/null -w '%{http_code}' "$BASIS/health" || true)" = "200" ]; then
    break
  fi
  if [ "$(docker inspect -f '{{.State.Running}}' "$CONTAINER")" != "true" ]; then
    docker logs "$CONTAINER"
    echo "Container ist beendet"; exit 1
  fi
  sleep 1
done
if [ "$(curl -s "$BASIS/health" || true)" != "ok" ]; then
  docker logs "$CONTAINER"
  echo "/health antwortet nicht mit ok"; exit 1
fi
ok "/health → 200 ok"

# post <pfad> <datei> <ausgabe> → gibt den HTTP-Status aus
post() {
  curl -s -o "$3" -w '%{http_code}' -X POST -H 'Content-Type: application/xml' \
    --data-binary "@$2" "$BASIS$1" || echo "000"
}

pruefe_status() { # <beschreibung> <erwartet> <ist>
  if [ "$3" = "$2" ]; then ok "$1 → $3"; else fehler "$1: erwartet $2, erhalten $3"; fi
}

pruefe_inhalt() { # <beschreibung> <datei> <muster>
  if grep -q -- "$3" "$2"; then ok "$1"; else fehler "$1 (\"$3\" fehlt)"; fi
}

pruefe_nicht() { # <beschreibung> <datei> <muster>
  if grep -q -- "$3" "$2"; then fehler "$1 (\"$3\" gefunden)"; else ok "$1"; fi
}

echo "== /validate"
s="$(post /validate "$TESTDATEN/xrechnung-cii.xml" "$ARBEIT/cii.xml")"
pruefe_status "gültige XRechnung CII" 200 "$s"
pruefe_inhalt "Bericht CII ist rep:report" "$ARBEIT/cii.xml" "<rep:report"

s="$(post /validate "$TESTDATEN/xrechnung-ubl.xml" "$ARBEIT/ubl.xml")"
pruefe_status "gültige XRechnung UBL" 200 "$s"

s="$(post /validate "$TESTDATEN/xrechnung-ohne-kaeuferreferenz.xml" "$ARBEIT/ohne.xml")"
pruefe_status "XRechnung ohne Käuferreferenz" 406 "$s"
pruefe_inhalt "Bericht nennt BR-DE-15" "$ARBEIT/ohne.xml" "BR-DE-15"

s="$(post /validate "$TESTDATEN/kein-xml.txt" "$ARBEIT/keinxml.xml")"
pruefe_status "kein XML" 406 "$s"
pruefe_inhalt "Bericht für kein XML ist rep:report" "$ARBEIT/keinxml.xml" "<rep:report"

: > "$ARBEIT/leer.xml"
s="$(post /validate "$ARBEIT/leer.xml" "$ARBEIT/leer-antwort.txt")"
pruefe_status "leerer Body /validate" 400 "$s"

s="$(post /validate "$TESTDATEN/externe-entitaet.xml" "$ARBEIT/xxe-validate.xml")"
pruefe_status "externe Entität /validate abgelehnt" 406 "$s"
pruefe_nicht "externe Entität /validate nicht aufgelöst" "$ARBEIT/xxe-validate.xml" "root:x:0:0"

echo "== /visualize"
s="$(post /visualize "$TESTDATEN/xrechnung-cii.xml" "$ARBEIT/cii.html")"
pruefe_status "Visualisierung CII" 200 "$s"
pruefe_inhalt "HTML CII enthält Rechnungsnummer" "$ARBEIT/cii.html" "123456XX"
pruefe_inhalt "HTML CII ist HTML" "$ARBEIT/cii.html" "<html"
if grep -qiE '<(link|script|img|iframe)[^>]*(src|href)=' "$ARBEIT/cii.html"; then
  fehler "HTML CII verweist auf externe Dateien"
else
  ok "HTML CII ist in sich geschlossen"
fi

s="$(post /visualize "$TESTDATEN/xrechnung-ubl.xml" "$ARBEIT/ubl.html")"
pruefe_status "Visualisierung UBL" 200 "$s"
pruefe_inhalt "HTML UBL enthält Rechnungsnummer" "$ARBEIT/ubl.html" "123456XX"

ct="$(curl -s -o /dev/null -w '%{content_type}' -X POST --data-binary "@$TESTDATEN/xrechnung-ubl.xml" "$BASIS/visualize")"
pruefe_status "Content-Type /visualize" "text/html; charset=utf-8" "$ct"

s="$(post /visualize "$TESTDATEN/kein-xml.txt" "$ARBEIT/keinxml.html")"
pruefe_status "Visualisierung kein XML" 422 "$s"

s="$(post /visualize "$ARBEIT/leer.xml" "$ARBEIT/leer-vis.txt")"
pruefe_status "leerer Body /visualize" 400 "$s"

s="$(post /visualize "$TESTDATEN/externe-entitaet.xml" "$ARBEIT/xxe.html")"
pruefe_status "externe Entität /visualize abgelehnt" 422 "$s"
pruefe_nicht "externe Entität /visualize nicht aufgelöst" "$ARBEIT/xxe.html" "root:x:0:0"

echo
if [ "$FEHLER" -gt 0 ]; then
  echo "$FEHLER Prüfung(en) fehlgeschlagen"
  docker logs --tail 50 "$CONTAINER" || true
  exit 1
fi
echo "Alle Prüfungen bestanden"
