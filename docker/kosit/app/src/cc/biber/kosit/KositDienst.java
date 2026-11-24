package cc.biber.kosit;

import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * HTTP-Sidecar für KoSIT-Validierung und XRechnung-Visualisierung.
 *
 * <ul>
 *   <li>{@code POST /validate} – 200 (akzeptiert) bzw. 406 (abgelehnt) mit KoSIT-Prüfbericht, 400 bei leerem Body</li>
 *   <li>{@code POST /visualize} – 200 mit HTML, 422 wenn nicht darstellbar, 400 bei leerem Body</li>
 *   <li>{@code GET /health} – 200 mit {@code ok}</li>
 * </ul>
 *
 * Konfiguration über Umgebungsvariablen: {@code KOSIT_PORT} (8080), {@code KOSIT_SZENARIEN}
 * (Ordner mit {@code scenarios.xml}), {@code KOSIT_VISUALISIERUNG} (Ordner mit den XSL-Dateien),
 * {@code KOSIT_THREADS} (Größe des Thread-Pools).
 */
public final class KositDienst {

    static final int MAX_BYTES = 20 * 1024 * 1024;

    private static final Logger LOG = Logger.getLogger(KositDienst.class.getName());

    private final Validierung validierung;
    private final Visualisierung visualisierung;

    KositDienst(Validierung validierung, Visualisierung visualisierung) {
        this.validierung = validierung;
        this.visualisierung = visualisierung;
    }

    public static void main(String[] args) throws Exception {
        int port = Integer.parseInt(env("KOSIT_PORT", "8080"));
        Path szenarien = Path.of(env("KOSIT_SZENARIEN", "/opt/kosit/szenarien"));
        Path visualisierung = Path.of(env("KOSIT_VISUALISIERUNG", "/opt/kosit/visualisierung/xsl"));
        int threads = Integer.parseInt(env("KOSIT_THREADS",
                String.valueOf(Math.max(4, Runtime.getRuntime().availableProcessors() * 2))));

        // Szenarien und Stylesheets werden vor dem Start des Servers geladen bzw. kompiliert;
        // /health antwortet also erst, wenn beides bereitsteht.
        KositDienst dienst = new KositDienst(new Validierung(szenarien), new Visualisierung(visualisierung));

        ExecutorService pool = Executors.newFixedThreadPool(threads);
        HttpServer server = HttpServer.create(new InetSocketAddress(port), 0);
        server.createContext("/", dienst::bearbeite);
        server.setExecutor(pool);
        server.start();
        LOG.info(() -> "KoSIT-Sidecar lauscht auf Port " + port + " mit " + threads + " Threads");

        Runtime.getRuntime().addShutdownHook(new Thread(() -> {
            server.stop(2);
            pool.shutdown();
        }));
    }

    private void bearbeite(HttpExchange exchange) throws IOException {
        try (exchange) {
            String pfad = exchange.getRequestURI().getPath();
            String methode = exchange.getRequestMethod();
            switch (pfad) {
                case "/health" -> {
                    if (!erlaube(exchange, methode, "GET")) return;
                    sende(exchange, 200, "text/plain; charset=utf-8", "ok".getBytes(StandardCharsets.UTF_8));
                }
                case "/validate" -> {
                    if (!erlaube(exchange, methode, "POST")) return;
                    byte[] body = leseBody(exchange);
                    if (body == null) return;
                    Validierung.Ergebnis ergebnis = validierung.pruefe(body);
                    sende(exchange, ergebnis.akzeptiert() ? 200 : 406, ergebnis.contentType(), ergebnis.inhalt());
                }
                case "/visualize" -> {
                    if (!erlaube(exchange, methode, "POST")) return;
                    byte[] body = leseBody(exchange);
                    if (body == null) return;
                    try {
                        sende(exchange, 200, "text/html; charset=utf-8", visualisierung.erzeugeHtml(body));
                    } catch (Visualisierung.NichtDarstellbarException e) {
                        sendeText(exchange, 422, "Nicht darstellbar: " + e.getMessage());
                    }
                }
                default -> sendeText(exchange, 404, "Nicht gefunden");
            }
        } catch (Exception e) {
            LOG.log(Level.SEVERE, "Fehler bei der Verarbeitung", e);
            try {
                sendeText(exchange, 500, "Interner Fehler");
            } catch (IOException ignored) {
                // Antwort war bereits begonnen
            }
        }
    }

    private static boolean erlaube(HttpExchange exchange, String methode, String erlaubt) throws IOException {
        if (erlaubt.equals(methode)) return true;
        exchange.getResponseHeaders().set("Allow", erlaubt);
        sendeText(exchange, 405, "Methode nicht erlaubt");
        return false;
    }

    /** Liest den Body; bei leerem oder zu großem Body wird direkt geantwortet und {@code null} geliefert. */
    private static byte[] leseBody(HttpExchange exchange) throws IOException {
        String laenge = exchange.getRequestHeaders().getFirst("Content-Length");
        if (laenge != null && Long.parseLong(laenge.trim()) > MAX_BYTES) {
            sendeText(exchange, 413, "Body größer als 20 MB");
            return null;
        }
        ByteArrayOutputStream puffer = new ByteArrayOutputStream();
        try (InputStream in = exchange.getRequestBody()) {
            byte[] block = new byte[64 * 1024];
            int gelesen;
            while ((gelesen = in.read(block)) != -1) {
                puffer.write(block, 0, gelesen);
                if (puffer.size() > MAX_BYTES) {
                    sendeText(exchange, 413, "Body größer als 20 MB");
                    return null;
                }
            }
        }
        byte[] body = puffer.toByteArray();
        if (body.length == 0 || new String(body, StandardCharsets.UTF_8).isBlank()) {
            sendeText(exchange, 400, "Leerer Body");
            return null;
        }
        return body;
    }

    private static void sendeText(HttpExchange exchange, int status, String text) throws IOException {
        sende(exchange, status, "text/plain; charset=utf-8", text.getBytes(StandardCharsets.UTF_8));
    }

    private static void sende(HttpExchange exchange, int status, String contentType, byte[] inhalt) throws IOException {
        exchange.getResponseHeaders().set("Content-Type", contentType);
        exchange.sendResponseHeaders(status, inhalt.length == 0 ? -1 : inhalt.length);
        if (inhalt.length > 0) {
            try (OutputStream out = exchange.getResponseBody()) {
                out.write(inhalt);
            }
        }
    }

    private static String env(String name, String standard) {
        String wert = System.getenv(name);
        return wert == null || wert.isBlank() ? standard : wert;
    }
}
