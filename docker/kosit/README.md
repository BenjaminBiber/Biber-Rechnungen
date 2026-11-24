# kosit

Lauffähiger Platzhalter: kleines Image, das auf Port 8080 mit 200 auf `/health` antwortet.
Wird vom Ticket für den KoSIT-Validator ersetzt; dabei ändert sich nur dieser Ordner, nicht `docker-compose.yml`.

Der Dienst hängt nur im internen Netz `intern` und veröffentlicht keinen Port. Die API erreicht ihn unter `http://kosit:8080`.
