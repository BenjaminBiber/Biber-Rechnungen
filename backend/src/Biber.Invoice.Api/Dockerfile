# API-Image mit eingebautem Frontend. Build-Kontext ist der Repo-Hauptordner:
#   docker build -f backend/src/Biber.Invoice.Api/Dockerfile .

# 1) Frontend bauen
FROM node:22-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2) API veröffentlichen
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src/backend
COPY backend/ ./
RUN dotnet publish src/Biber.Invoice.Api/Biber.Invoice.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# 3) Laufzeit
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS laufzeit
ARG APP_UID=1000
ARG APP_GID=1000

# Native Abhängigkeiten für PDFium (libc6, libstdc++6) und curl für den Healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libc6 libstdc++6 curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd --gid "${APP_GID}" biber 2>/dev/null || true \
    && useradd --uid "${APP_UID}" --gid "${APP_GID}" --no-create-home --shell /usr/sbin/nologin biber 2>/dev/null \
       || usermod --login biber "$(getent passwd "${APP_UID}" | cut -d: -f1)" \
    && mkdir -p /srv/rechnungen/eingang /srv/rechnungen/staging /srv/rechnungen/ablage \
                /srv/rechnungen/daten /srv/rechnungen/backup /app/verfahrensdokumentation \
    && chown -R "${APP_UID}:${APP_GID}" /srv/rechnungen

WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER ${APP_UID}:${APP_GID}

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Biber.Invoice.Api.dll"]
