# kosit

Sidecar für die Prüfung und Darstellung von E-Rechnungen (Plan Abschnitt 2 und 3, P1; R2-11, B2).
Kleiner Java-21-Dienst um den KoSIT-Validator, die XRechnung-Validator-Konfiguration und die
XRechnung-Visualisierung (Saxon-HE). Der Dienst hängt nur im internen Netz `intern` und veröffentlicht
keinen Port; die API erreicht ihn unter `http://kosit:8080`.

## HTTP-Schnittstelle

| Aufruf | Antwort |
| --- | --- |
| `POST /validate` (Body: XML, höchstens 20 MB) | `200` akzeptiert, `406` abgelehnt; Body jeweils der KoSIT-Prüfbericht (`rep:report`, `application/xml`). Kein XML oder kein passendes Szenario → `406` mit Bericht. Leerer Body → `400` (Text). |
| `POST /visualize` (Body: XRechnung CII oder UBL, Invoice oder CreditNote) | `200` mit `text/html; charset=utf-8`, in sich geschlossen (CSS und JS eingebettet). Nicht darstellbar (kein XML, unbekanntes Wurzelelement, Fehler in der Transformation) → `422` (Text). Leerer Body → `400` (Text). |
| `GET /health` | `200` mit `ok`; der Server startet erst, wenn Szenarien geladen und Stylesheets kompiliert sind. |

Body über 20 MB → `413`, falsche Methode → `405`, unbekannter Pfad → `404`.

XXE-Schutz: Für `/validate` liefert der Validator selbst einen Saxon-Prozessor, der DOCTYPE-Deklarationen
und externe DTDs/Entitäten verbietet; `/visualize` parst mit einem eigenen SAX-Parser mit denselben
Einschränkungen. Dokumente mit DOCTYPE werden abgelehnt (`406` bzw. `422`), Entitäten nie aufgelöst.

Anfragen laufen parallel über einen Thread-Pool (`KOSIT_THREADS`, Standard: 2 × CPU-Kerne, mindestens 4).

## Versionen und Quellen

| Artefakt | Version | Quelle | SHA-256 |
| --- | --- | --- | --- |
| KoSIT-Validator (Standalone-JAR inkl. Saxon-HE) | 1.6.3 | <https://github.com/itplr-kosit/validator/releases/tag/v1.6.3> (`validator-1.6.3-standalone.jar`) | `799e64befca97d4080e03608c80b85dd5a5ecc5f4ae4f35d1116ec2855b9a7c9` |
| XRechnung-Validator-Konfiguration (XRechnung 3.0.2) | 2026-08-31 | <https://github.com/itplr-kosit/validator-configuration-xrechnung/releases/tag/v2026-08-31> (`xrechnung-3.0.2-validator-configuration-2026-08-31.zip`) | `2530cd107c414511c5d0462ec10f886910395abfca820db82e83d70bf01221a8` |
| XRechnung-Visualisierung (XRechnung 3.0.2) | 2026-10-13 | <https://github.com/itplr-kosit/xrechnung-visualization/releases/tag/v2026-10-13> (`xrechnung-3.0.2-visualization-2026-10-13.zip`) | `ecf3913029218822ee4c90a86a3e23689768bf3568a061cf9bc742115e52264e` |

Stand 2026-11-24 sind das die jeweils neuesten Releases; Konfiguration und Visualisierung gehören beide
zu XRechnung 3.0.2, die Konfiguration nennt Validator 1.6.x als Voraussetzung. Versionen stehen als
`ARG` im `Dockerfile`, die Prüfsummen daneben (`ADD --checksum` und zusätzlich `sha256sum -c`).
Beim Aktualisieren Version **und** Prüfsumme ändern (Prüfsumme steht im GitHub-Release unter „digest“).

Der Dienst wird ohne Maven/Gradle direkt mit `javac` gegen das Standalone-JAR übersetzt: Das JAR bringt
alle Bibliotheken mit (Validator, Saxon-HE, JAXB, SLF4J), so ist jede Abhängigkeit über eine Prüfsumme
festgelegt und der Build lädt nichts Ungeprüftes nach.

## Aufbau

- `app/src/cc/biber/kosit/` – `KositDienst` (HTTP-Server, `com.sun.net.httpserver`), `Validierung`, `Visualisierung`
- `Dockerfile` – Build-Stufe `eclipse-temurin:21-jdk` (Artefakte laden, prüfen, entpacken, übersetzen),
  Laufzeit-Stufe `eclipse-temurin:21-jre` mit Benutzer `kosit`, `EXPOSE 8080`, `HEALTHCHECK` auf `/health`
- `test/` – Beispielrechnungen
- `test.sh` – baut das Image, startet es auf einem freien Port und prüft die Schnittstelle mit `curl`

## Testdaten

| Datei | Inhalt |
| --- | --- |
| `test/xrechnung-cii.xml` | `instances/standard/01.01a-INVOICE_uncefact.xml` aus der XRechnung-Testsuite, Rechnungsnummer `123456XX` |
| `test/xrechnung-ubl.xml` | `instances/standard/01.01a-INVOICE_ubl.xml` aus der XRechnung-Testsuite, Rechnungsnummer `123456XX` |
| `test/xrechnung-ohne-kaeuferreferenz.xml` | wie `xrechnung-ubl.xml`, ohne `cbc:BuyerReference` (verletzt `BR-DE-15`) |
| `test/kein-xml.txt` | einfacher Text |
| `test/externe-entitaet.xml` | selbst erstellt, DOCTYPE mit externer Entität auf `file:///etc/passwd` |

Quelle der Testsuite: <https://github.com/itplr-kosit/xrechnung-testsuite/releases/tag/v2026-08-31>
(`xrechnung-3.0.2-testsuite-2026-08-31.zip`, Apache License 2.0).

## Test

```bash
bash docker/kosit/test.sh
```
