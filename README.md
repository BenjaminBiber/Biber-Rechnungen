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
docker compose up -d
```

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

Dokumentation prüfen:

```bash
node docs/pruefe-doku.mjs
npx --yes markdownlint-cli2 "README.md" "docs/betrieb.md" "docs/verfahrensdokumentation/*.md" "docker/**/*.md"
```

Stack: .NET 10 (Minimal APIs, EF Core, Hangfire) · PostgreSQL · React (Vite, TypeScript) · Docker Compose
