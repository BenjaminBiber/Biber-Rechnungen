# Biber-Rechnungen

Selbst gehostetes, GoBD-orientiertes Belegarchiv für Eingangsrechnungen: Import per E-Mail, Scanner-/Eingangsordner und Upload, Auslesen von E-Rechnungen (ZUGFeRD/XRechnung) und PDFs (Azure Document Intelligence), Plausibilitätsprüfungen, unveränderbare Ablage, Auswertung von Kosten, Korrespondenten und Artikeln. Mit MCP-Server und PWA.

**Status:** Planung abgeschlossen, Umsetzung noch nicht begonnen.

- Plan: [`docs/plan.md`](docs/plan.md)
- Verfahrensdokumentation (Entwurf): [`docs/verfahrensdokumentation/`](docs/verfahrensdokumentation/verfahrensdokumentation.md)
- Entscheidungsprotokoll und Fragebögen: [`planung/`](planung/entscheidungen.md)

Stack: .NET 10 (Minimal APIs, EF Core, Hangfire) · PostgreSQL · React (Vite, TypeScript) · Docker Compose
