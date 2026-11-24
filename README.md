# Biber-Rechnungen

Selbst gehostetes, GoBD-orientiertes Belegarchiv für Eingangsrechnungen: Import per E-Mail, Scanner-/Eingangsordner und Upload, Auslesen von E-Rechnungen (ZUGFeRD/XRechnung) und PDFs (Azure Document Intelligence), Plausibilitätsprüfungen, unveränderbare Ablage, Auswertung von Kosten, Korrespondenten und Artikeln. Mit MCP-Server und PWA.

**Status:** Planung abgeschlossen, Umsetzung noch nicht begonnen.

- Plan: [`docs/plan.md`](docs/plan.md)
- Verfahrensdokumentation (Entwurf): [`docs/verfahrensdokumentation/`](docs/verfahrensdokumentation/verfahrensdokumentation.md)
- Entscheidungsprotokoll und Fragebögen: [`planung/`](planung/entscheidungen.md)

## Starten

Voraussetzung: Docker mit Compose v2.

1. Konfiguration anlegen und Werte anpassen (mindestens `POSTGRES_PASSWORD`; Azure, IMAP und Google dürfen leer bleiben):
   ```bash
   cp .env.example .env
   ```
2. Verzeichnisse auf dem Host anlegen und dem Container-Benutzer (`APP_UID`/`APP_GID`) übergeben:
   ```bash
   sudo mkdir -p /srv/rechnungen/{eingang,staging,ablage,daten,backup}
   sudo chown -R 1000:1000 /srv/rechnungen
   ```
3. Starten:
   ```bash
   docker compose up -d
   ```
   Die Anwendung ist danach unter `http://<host>:${API_PORT}` erreichbar (Standard 8080).

Smoke-Test ohne Host-Verzeichnisse: `bash docker/smoke.sh`.

Stack: .NET 10 (Minimal APIs, EF Core, Hangfire) · PostgreSQL · React (Vite, TypeScript) · Docker Compose
