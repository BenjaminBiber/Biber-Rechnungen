# Platzhalter für den KoSIT-Validator: antwortet auf Port 8080 mit 200 auf /health.
# Wird vom KoSIT-Ticket durch den echten Validator ersetzt.
FROM busybox:1.37
RUN mkdir -p /www && echo ok > /www/health
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --retries=10 \
    CMD wget -q -O /dev/null http://localhost:8080/health || exit 1
CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]
