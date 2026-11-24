package cc.biber.kosit;

import net.sf.saxon.s9api.DocumentBuilder;
import net.sf.saxon.s9api.Processor;
import net.sf.saxon.s9api.QName;
import net.sf.saxon.s9api.SaxonApiException;
import net.sf.saxon.s9api.Serializer;
import net.sf.saxon.s9api.XdmDestination;
import net.sf.saxon.s9api.XdmNode;
import net.sf.saxon.s9api.XdmNodeKind;
import net.sf.saxon.s9api.Xslt30Transformer;
import net.sf.saxon.s9api.XsltCompiler;
import net.sf.saxon.s9api.XsltExecutable;
import org.xml.sax.InputSource;
import org.xml.sax.SAXException;
import org.xml.sax.XMLReader;

import javax.xml.XMLConstants;
import javax.xml.parsers.ParserConfigurationException;
import javax.xml.parsers.SAXParserFactory;
import javax.xml.transform.sax.SAXSource;
import javax.xml.transform.stream.StreamSource;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.nio.file.Path;

/**
 * Stellt XRechnungen (CII oder UBL, Invoice oder CreditNote) als HTML dar.
 *
 * Kette aus itplr-kosit/xrechnung-visualization: erst {@code cii-xr.xsl}, {@code ubl-invoice-xr.xsl}
 * oder {@code ubl-creditnote-xr.xsl} (je nach Wurzelelement) ins semantische Modell, dann
 * {@code xrechnung-html.xsl}. Die Stylesheets werden einmal beim Start kompiliert; ein
 * {@link XsltExecutable} ist threadsicher, je Anfrage entsteht ein eigener Transformer.
 */
final class Visualisierung {

    static final class NichtDarstellbarException extends Exception {
        NichtDarstellbarException(String meldung) {
            super(meldung);
        }
    }

    private static final String NS_CII = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    private static final String NS_UBL_INVOICE = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private static final String NS_UBL_CREDITNOTE = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    private final Processor processor;
    private final SAXParserFactory parserFabrik;
    private final XsltExecutable cii;
    private final XsltExecutable ublInvoice;
    private final XsltExecutable ublCreditNote;
    private final XsltExecutable html;

    Visualisierung(Path xslOrdner) throws SaxonApiException, ParserConfigurationException, SAXException {
        processor = new Processor(false);
        parserFabrik = sichererParser();
        XsltCompiler compiler = processor.newXsltCompiler();
        cii = kompiliere(compiler, xslOrdner, "cii-xr.xsl");
        ublInvoice = kompiliere(compiler, xslOrdner, "ubl-invoice-xr.xsl");
        ublCreditNote = kompiliere(compiler, xslOrdner, "ubl-creditnote-xr.xsl");
        html = kompiliere(compiler, xslOrdner, "xrechnung-html.xsl");
    }

    byte[] erzeugeHtml(byte[] xml) throws NichtDarstellbarException {
        XdmNode dokument = parse(xml);
        XsltExecutable zuModell = waehleStylesheet(dokument);
        try {
            XdmDestination modell = new XdmDestination();
            transformiere(zuModell, dokument, modell);

            ByteArrayOutputStream ausgabe = new ByteArrayOutputStream();
            Xslt30Transformer zuHtml = html.load30();
            Serializer serializer = zuHtml.newSerializer(ausgabe);
            serializer.setOutputProperty(Serializer.Property.ENCODING, "UTF-8");
            XdmNode modellDokument = modell.getXdmNode();
            zuHtml.setGlobalContextItem(modellDokument);
            zuHtml.applyTemplates(modellDokument, serializer);
            return ausgabe.toByteArray();
        } catch (SaxonApiException e) {
            throw new NichtDarstellbarException(e.getMessage());
        }
    }

    private static void transformiere(XsltExecutable stylesheet, XdmNode eingabe, XdmDestination ziel)
            throws SaxonApiException {
        Xslt30Transformer transformer = stylesheet.load30();
        transformer.setGlobalContextItem(eingabe);
        transformer.applyTemplates(eingabe, ziel);
    }

    /** Parst mit einem Parser, der DOCTYPE, externe Entitäten und externe DTDs verbietet. */
    private XdmNode parse(byte[] xml) throws NichtDarstellbarException {
        try {
            XMLReader reader = parserFabrik.newSAXParser().getXMLReader();
            reader.setEntityResolver((publicId, systemId) -> {
                throw new SAXException("Externe Entitäten sind nicht erlaubt");
            });
            DocumentBuilder builder = processor.newDocumentBuilder();
            return builder.build(new SAXSource(reader, new InputSource(new ByteArrayInputStream(xml))));
        } catch (SaxonApiException | SAXException | ParserConfigurationException e) {
            throw new NichtDarstellbarException("Kein gültiges XML: " + e.getMessage());
        }
    }

    private XsltExecutable waehleStylesheet(XdmNode dokument) throws NichtDarstellbarException {
        XdmNode wurzel = null;
        for (XdmNode kind : dokument.children()) {
            if (kind.getNodeKind() == XdmNodeKind.ELEMENT) {
                wurzel = kind;
                break;
            }
        }
        if (wurzel == null) {
            throw new NichtDarstellbarException("Kein Wurzelelement");
        }
        QName name = wurzel.getNodeName();
        String ns = name.getNamespace();
        if (NS_CII.equals(ns) && "CrossIndustryInvoice".equals(name.getLocalName())) return cii;
        if (NS_UBL_INVOICE.equals(ns) && "Invoice".equals(name.getLocalName())) return ublInvoice;
        if (NS_UBL_CREDITNOTE.equals(ns) && "CreditNote".equals(name.getLocalName())) return ublCreditNote;
        throw new NichtDarstellbarException("Unbekanntes Wurzelelement {" + ns + "}" + name.getLocalName());
    }

    private static XsltExecutable kompiliere(XsltCompiler compiler, Path ordner, String datei) throws SaxonApiException {
        return compiler.compile(new StreamSource(ordner.resolve(datei).toFile()));
    }

    private static SAXParserFactory sichererParser() throws ParserConfigurationException, SAXException {
        SAXParserFactory fabrik = SAXParserFactory.newInstance();
        fabrik.setNamespaceAware(true);
        fabrik.setXIncludeAware(false);
        fabrik.setFeature(XMLConstants.FEATURE_SECURE_PROCESSING, true);
        fabrik.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
        fabrik.setFeature("http://xml.org/sax/features/external-general-entities", false);
        fabrik.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
        fabrik.setFeature("http://apache.org/xml/features/nonvalidating/load-external-dtd", false);
        return fabrik;
    }
}
