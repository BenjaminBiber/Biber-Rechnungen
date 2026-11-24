package cc.biber.kosit;

import de.kosit.validationtool.api.Check;
import de.kosit.validationtool.api.Configuration;
import de.kosit.validationtool.api.InputFactory;
import de.kosit.validationtool.api.Result;
import de.kosit.validationtool.impl.DefaultCheck;
import de.kosit.validationtool.impl.xml.ProcessorProvider;
import net.sf.saxon.s9api.Processor;
import net.sf.saxon.s9api.SaxonApiException;
import net.sf.saxon.s9api.Serializer;

import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

/**
 * Prüft Rechnungen mit dem KoSIT-Validator gegen die XRechnung-Konfiguration.
 *
 * Der Saxon-Prozessor kommt vom {@link ProcessorProvider} des Validators: Er verbietet
 * DOCTYPE-Deklarationen und das Laden externer DTDs und Entitäten (XXE-Schutz).
 * {@link DefaultCheck} ist für parallele Aufrufe ausgelegt.
 */
final class Validierung {

    record Ergebnis(boolean akzeptiert, String contentType, byte[] inhalt) {
    }

    private final Processor processor;
    private final Check pruefung;

    Validierung(Path szenarienOrdner) {
        Path szenarien = szenarienOrdner.resolve("scenarios.xml");
        if (!Files.isRegularFile(szenarien)) {
            throw new IllegalStateException("scenarios.xml fehlt in " + szenarienOrdner);
        }
        processor = ProcessorProvider.getProcessor();
        Configuration konfiguration = Configuration
                .load(szenarien.toUri(), szenarienOrdner.toUri())
                .build(processor);
        pruefung = new DefaultCheck(processor, konfiguration);
    }

    Ergebnis pruefe(byte[] xml) throws SaxonApiException {
        Result ergebnis = pruefung.checkInput(InputFactory.read(xml, "rechnung.xml"));
        boolean akzeptiert = ergebnis.isAcceptable();
        if (ergebnis.getReport() == null) {
            String fehler = "Kein Prüfbericht: " + String.join("; ", ergebnis.getProcessingErrors());
            return new Ergebnis(false, "text/plain; charset=utf-8", fehler.getBytes(StandardCharsets.UTF_8));
        }
        ByteArrayOutputStream ausgabe = new ByteArrayOutputStream();
        Serializer serializer = processor.newSerializer(ausgabe);
        serializer.setOutputProperty(Serializer.Property.METHOD, "xml");
        serializer.setOutputProperty(Serializer.Property.ENCODING, "UTF-8");
        serializer.serializeNode(ergebnis.getReport());
        return new Ergebnis(akzeptiert, "application/xml; charset=utf-8", ausgabe.toByteArray());
    }
}
