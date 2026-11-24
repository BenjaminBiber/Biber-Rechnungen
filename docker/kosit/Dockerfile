# KoSIT-Sidecar: Validierung (KoSIT-Validator + XRechnung-Konfiguration) und
# HTML-Visualisierung (xrechnung-visualization mit Saxon-HE) über HTTP auf Port 8080.
# Versionen und Quellen: README.md. Jede Datei wird über ihre SHA-256-Prüfsumme geprüft.

ARG VALIDATOR_VERSION=1.6.3
ARG KONFIGURATION_VERSION=2026-08-31
ARG VISUALISIERUNG_VERSION=2026-10-13

# ---- Build-Stufe: Artefakte laden, prüfen, entpacken; Dienst übersetzen ----
FROM eclipse-temurin:21-jdk AS build
ARG VALIDATOR_VERSION
ARG KONFIGURATION_VERSION
ARG VISUALISIERUNG_VERSION

WORKDIR /build

# KoSIT-Validator (Standalone-JAR, enthält Saxon-HE und alle Abhängigkeiten)
ADD --checksum=sha256:799e64befca97d4080e03608c80b85dd5a5ecc5f4ae4f35d1116ec2855b9a7c9 \
    https://github.com/itplr-kosit/validator/releases/download/v${VALIDATOR_VERSION}/validator-${VALIDATOR_VERSION}-standalone.jar \
    /build/validator.jar
# XRechnung-Validator-Konfiguration (scenarios.xml, Schemata, Schematron)
ADD --checksum=sha256:2530cd107c414511c5d0462ec10f886910395abfca820db82e83d70bf01221a8 \
    https://github.com/itplr-kosit/validator-configuration-xrechnung/releases/download/v${KONFIGURATION_VERSION}/xrechnung-3.0.2-validator-configuration-${KONFIGURATION_VERSION}.zip \
    /build/konfiguration.zip
# XRechnung-Visualisierung (XSLT)
ADD --checksum=sha256:ecf3913029218822ee4c90a86a3e23689768bf3568a061cf9bc742115e52264e \
    https://github.com/itplr-kosit/xrechnung-visualization/releases/download/v${VISUALISIERUNG_VERSION}/xrechnung-3.0.2-visualization-${VISUALISIERUNG_VERSION}.zip \
    /build/visualisierung.zip

# Prüfsummen zusätzlich explizit prüfen (bricht den Build bei Abweichung ab)
RUN printf '%s  %s\n' \
      799e64befca97d4080e03608c80b85dd5a5ecc5f4ae4f35d1116ec2855b9a7c9 validator.jar \
      2530cd107c414511c5d0462ec10f886910395abfca820db82e83d70bf01221a8 konfiguration.zip \
      ecf3913029218822ee4c90a86a3e23689768bf3568a061cf9bc742115e52264e visualisierung.zip \
    | sha256sum -c -

RUN mkdir -p /opt/kosit/szenarien /opt/kosit/visualisierung \
 && cd /opt/kosit/szenarien && jar xf /build/konfiguration.zip \
 && cd /opt/kosit/visualisierung && jar xf /build/visualisierung.zip \
 && test -f /opt/kosit/szenarien/scenarios.xml \
 && test -f /opt/kosit/visualisierung/xsl/xrechnung-html.xsl

COPY app/src /build/src
RUN javac --release 21 -encoding UTF-8 -cp validator.jar -d classes $(find src -name '*.java') \
 && printf 'Main-Class: cc.biber.kosit.KositDienst\nClass-Path: validator.jar\n' > manifest.txt \
 && jar cfm /opt/kosit/kosit-dienst.jar manifest.txt -C classes . \
 && cp validator.jar /opt/kosit/validator.jar \
 && chmod -R a+rX,go-w /opt/kosit

# ---- Laufzeit-Stufe ----
FROM eclipse-temurin:21-jre

RUN groupadd --system kosit && useradd --system --gid kosit --no-create-home --shell /usr/sbin/nologin kosit
COPY --from=build --chown=root:root /opt/kosit /opt/kosit

ENV KOSIT_PORT=8080 \
    KOSIT_SZENARIEN=/opt/kosit/szenarien \
    KOSIT_VISUALISIERUNG=/opt/kosit/visualisierung/xsl \
    JAVA_TOOL_OPTIONS="-XX:MaxRAMPercentage=75 -Djava.net.preferIPv4Stack=true"

USER kosit
WORKDIR /opt/kosit
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=3s --start-period=60s --retries=5 \
    CMD wget -q -O /dev/null http://localhost:8080/health || exit 1
ENTRYPOINT ["java", "-jar", "/opt/kosit/kosit-dienst.jar"]
