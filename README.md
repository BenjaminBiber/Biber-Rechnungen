# Biber-Rechnungen

Selbst gehostetes, GoBD-orientiertes Belegarchiv für Eingangsrechnungen: Import per E-Mail, Scanner-/Eingangsordner und Upload, Auslesen von E-Rechnungen (ZUGFeRD/XRechnung) und PDFs (Azure Document Intelligence), Plausibilitätsprüfungen, unveränderbare Ablage, Auswertung von Kosten, Korrespondenten und Artikeln. Mit MCP-Server und PWA. Läuft mit Docker Compose (`api`, `postgres`, `kosit`, `db-backup`) neben einem vorhandenen Samba-Container und Nginx Proxy Manager, nur im LAN.

## Dokumentation

- Einrichtung und Betrieb (Verzeichnisse, `.env`, Samba, Proxy, Backup, Wiederherstellung): [`docs/betrieb.md`](docs/betrieb.md)
- Plan (Architektur, Datenmodell, Pipeline): [`docs/plan.md`](docs/plan.md)
- Verfahrensdokumentation (GoBD): [`docs/verfahrensdokumentation/verfahrensdokumentation.md`](docs/verfahrensdokumentation/verfahrensdokumentation.md)
- Samba-Beispielkonfiguration: [`docker/samba/README.md`](docker/samba/README.md)
- Entscheidungsprotokoll und Fragebögen: [`planung/entscheidungen.md`](planung/entscheidungen.md)

## Schnellstart

```bash
cp .env.example .env            # mindestens POSTGRES_PASSWORD ändern
sudo mkdir -p /srv/rechnungen/{eingang,staging,ablage,daten,backup}
sudo chown -R 1000:1000 /srv/rechnungen
docker compose pull
docker compose up -d
```

Die Images kommen von Docker Hub (`benjaminbiber/biber-rechnungen-*`, Version über `BIBER_VERSION` in `.env`).

Ausführlich in [`docs/betrieb.md`](docs/betrieb.md). Smoke-Test ohne Host-Verzeichnisse: `bash docker/smoke.sh`.

## Entwicklung

Backend (.NET 10, aus dem Repo-Hauptordner; Integrationstests brauchen Docker):

```bash
dotnet build backend/Biber.Invoice.sln -warnaserror
dotnet test backend/Biber.Invoice.sln
dotnet format backend/Biber.Invoice.sln --verify-no-changes
```

Frontend (in `frontend/`):

```bash
npm ci
npm run dev           # Entwicklungsserver
npm run lint
npm run typecheck
npm run test
npm run build
npm run e2e           # Playwright gegen npm run preview:mock
```

Lokal debuggen (z. B. in Rider): PostgreSQL, KoSIT und ein Test-Postfach (GreenMail; Mails per SMTP an `localhost:3025`, Empfänger `rechnungen@dev.local`) laufen in Docker, die API im Profil `Biber.Invoice.Api` aus `Properties/launchSettings.json` (Umgebung `Development`, `http://localhost:8080`), das Frontend über den Vite-Server, der `/api` an die API weiterleitet:

```bash
docker compose -f docker/compose.dev.yml up -d --build --wait
cd frontend && npm run dev   # danach http://localhost:5173 öffnen
```

In Rider (Solution `backend/Biber.Invoice.sln`) geht das in einem Schritt: die Run-Konfiguration **Entwicklung (API + Frontend)** aus `backend/.run/` startet API und Vite zusammen; vor der API startet sie die Docker-Dienste (`npm run dev:docker` in `frontend/`). Mit Debug läuft die API im Debugger. Einzeln gibt es **API**, **Frontend (Vite)** und **Docker-Dienste (PostgreSQL, KoSIT, Mail)**.

Laufzeitdaten landen in `backend/src/Biber.Invoice.Api/.daten/`. Mit `docker compose -f docker/compose.dev.yml down -v` wird die Datenbank geleert.

Dokumentation prüfen:

```bash
node docs/pruefe-doku.mjs
npx --yes markdownlint-cli2 "README.md" "docs/betrieb.md" "docs/verfahrensdokumentation/*.md" "docker/**/*.md"
```

## Release

Ein Release baut die drei eigenen Images und pusht sie nach Docker Hub, damit der Stack (z. B. in Portainer) ohne eigenen Build läuft.

1. Auf GitHub **Actions → Release → Run workflow** öffnen.
2. Branch `main` lassen (auf anderen Branches bricht der Workflow ab) und die Stufe wählen: `patch` (Standard), `minor` oder `major`.
3. Der Workflow prüft Branch und Secrets, berechnet die Version, lässt die CI (Backend, Frontend, Docker-Smoke-Test) laufen, baut und pusht die Images und legt erst danach den Git-Tag `vX.Y.Z` und das GitHub-Release mit automatisch erzeugten Notes an. Ist ein Schritt rot, wird nichts veröffentlicht.

Nötige Repository-Secrets (Settings → Secrets and variables → Actions):

- `DOCKERHUB_USERNAME`: Docker-Hub-Benutzer
- `DOCKERHUB_PASSWORD`: Passwort, besser ein Docker-Hub-Access-Token mit Schreibrecht

| Dienst | Image | Tags |
| --- | --- | --- |
| api (mit Frontend) | `benjaminbiber/biber-rechnungen-api` | `latest`, `X.Y.Z` |
| kosit | `benjaminbiber/biber-rechnungen-kosit` | `latest`, `X.Y.Z` |
| db-backup | `benjaminbiber/biber-rechnungen-db-backup` | `latest`, `X.Y.Z` |

Versionierung: Quelle sind die Git-Tags `vX.Y.Z`; ohne Tag startet die Zählung bei `0.0.0`. `major` setzt Minor und Patch auf 0, `minor` setzt Patch auf 0. Es gibt keinen Commit zurück auf `main`, `frontend/package.json` bleibt bei `0.0.0`. Das api-Image trägt die Version als Assembly-Version (Build-Argument `APP_VERSION`); ein lokaler Build ohne Argument ergibt `0.0.0`. Die Rechnung lässt sich lokal prüfen: `bash .github/scripts/naechste-version.test.sh`.

Die Images laufen als Benutzer mit UID/GID `1000`. Die Host-Ordner (z. B. `/srv/rechnungen`) müssen deshalb UID/GID `1000` gehören.

Stack: .NET 10 (Minimal APIs, EF Core, Hangfire) · PostgreSQL · React (Vite, TypeScript) · Docker Compose
