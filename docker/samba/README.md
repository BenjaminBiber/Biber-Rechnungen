# Samba-Freigaben

Biber-Rechnungen startet keinen eigenen Samba. Die Datei [`smb.conf.example`](smb.conf.example) enthält die beiden Freigaben für den **vorhandenen** Samba-Container:

| Freigabe | Pfad | Zugriff |
| --- | --- | --- |
| `Rechnungen-Eingang` | `/srv/rechnungen/eingang` | lesen und schreiben (Scanner, Benutzer) |
| `Rechnungen-Ablage` | `/srv/rechnungen/ablage` | nur lesen, `vfs objects = worm` |

## Einbindung

1. Die Host-Ordner `PFAD_EINGANG` und `PFAD_ABLAGE` aus `.env` in den Samba-Container unter denselben Pfaden mounten (`/srv/rechnungen/eingang`, `/srv/rechnungen/ablage`).
2. Im Samba-Container ein Konto und eine Gruppe `rechnungen` mit UID/GID wie `APP_UID`/`APP_GID` anlegen und Scanner- sowie Benutzerkonten der Gruppe hinzufügen (`smbpasswd -a <name>`).
3. Die beiden Abschnitte `[Rechnungen-Eingang]` und `[Rechnungen-Ablage]` in die `smb.conf` des Containers übernehmen:
   - Images mit vollständiger `smb.conf` als Volume (z. B. `dperson/samba`, `crazymax/samba` mit eigener Datei): Datei per Volume nach `/etc/samba/smb.conf` einhängen, z. B. `- ./smb.conf:/etc/samba/smb.conf:ro`.
   - Images mit Konfiguration über Umgebungsvariablen oder YAML (z. B. `crazymax/samba` mit `config.yml`, `servercontainers/samba`): die Optionen der beiden Freigaben dort als Freigabe-Optionen eintragen.
4. Konfiguration prüfen und Container neu starten:

   ```bash
   docker exec <samba-container> testparm -s
   docker restart <samba-container>
   ```

Prüfung der Beispieldatei ohne vorhandenen Samba:

```bash
docker run --rm -v "$PWD/docker/samba/smb.conf.example:/etc/samba/smb.conf:ro" alpine:3 \
  sh -c "apk add --no-cache samba-common-tools >/dev/null && mkdir -p /srv/rechnungen/eingang /srv/rechnungen/ablage /var/lib/samba /var/cache/samba /run/samba && testparm -s"
```

Die Samba-Verzeichnisse `/var/lib/samba`, `/var/cache/samba` und `/run/samba` fehlen im nackten Alpine-Image; ohne sie meldet `testparm` Fehler zur Umgebung, nicht zur Konfiguration.

## Grenzen des WORM-Schutzes

Die App schreibt über ihren Bind-Mount direkt auf den Host, nicht über SMB. Das WORM-Modul schützt deshalb nur den Zugriff über die Freigabe. Gegen Änderungen direkt auf dem Host schützen die SHA-256-Prüfsumme jeder Datei und die nächtliche Integritätsprüfung (siehe [`docs/betrieb.md`](../../docs/betrieb.md)).
