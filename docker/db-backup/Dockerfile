# Platzhalter für die nächtliche Datenbanksicherung: schläft nur.
# Wird vom db-backup-Ticket durch das echte Sicherungs-Image ersetzt.
FROM busybox:1.37
HEALTHCHECK --interval=5s --timeout=3s --retries=3 CMD true
CMD ["sh", "-c", "trap 'exit 0' TERM INT; while :; do sleep 3600 & wait $!; done"]
