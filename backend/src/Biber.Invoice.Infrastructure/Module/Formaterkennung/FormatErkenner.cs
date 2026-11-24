using System.Xml;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using UglyToad.PdfPig;

namespace Biber.Invoice.Infrastructure.Module.Formaterkennung;

/// <summary>
/// Erkennt PDF, ZUGFeRD/Factur-X-PDF, XRechnung-XML (CII oder UBL) und Bilder (PNG, JPEG, TIFF)
/// anhand der Signatur am Dateianfang. Die Endung spielt keine Rolle.
/// </summary>
public sealed class FormatErkenner : IFormatErkenner
{
    public const string NamensraumCii = "urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100";
    public const string NamensraumUblRechnung = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string NamensraumUblGutschrift = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    private static readonly string[] ERechnungAnhaenge =
        ["factur-x.xml", "zugferd-invoice.xml", "xrechnung.xml"];

    public FormatErgebnis Erkennen(Stream datei, string dateiname)
    {
        byte[] inhalt;
        using (var puffer = new MemoryStream())
        {
            datei.CopyTo(puffer);
            inhalt = puffer.ToArray();
        }

        if (BeginntMit(inhalt, "%PDF"u8))
        {
            return ErkennePdf(inhalt);
        }

        if (BeginntMit(inhalt, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) || BeginntMit(inhalt, [0xFF, 0xD8, 0xFF]))
        {
            return new FormatErgebnis(ErkanntesFormat.Bild, null, 1);
        }

        if (BeginntMit(inhalt, "II*\0"u8) || BeginntMit(inhalt, "MM\0*"u8))
        {
            var seiten = TiffSeiten(inhalt);
            return seiten > 0 ? new FormatErgebnis(ErkanntesFormat.Bild, null, seiten) : FormatErgebnis.Unbekannt;
        }

        if (SiehtAusWieXml(inhalt))
        {
            return IstERechnungXml(inhalt)
                ? new FormatErgebnis(ErkanntesFormat.XRechnungXml, inhalt, 1)
                : FormatErgebnis.Unbekannt;
        }

        return FormatErgebnis.Unbekannt;
    }

    private static FormatErgebnis ErkennePdf(byte[] inhalt)
    {
        try
        {
            using var dokument = PdfDocument.Open(inhalt);
            var seiten = dokument.NumberOfPages;
            if (dokument.Advanced.TryGetEmbeddedFiles(out var anhaenge))
            {
                var xml = anhaenge.FirstOrDefault(a => IstERechnungAnhang(a.Name) || IstERechnungAnhang(a.FileSpecification));
                if (xml is not null)
                {
                    return new FormatErgebnis(ErkanntesFormat.ZugferdPdf, xml.Bytes.ToArray(), seiten);
                }
            }

            return new FormatErgebnis(ErkanntesFormat.Pdf, null, seiten);
        }
        catch (Exception)
        {
            // PdfPig wirft bei beschädigten Dateien verschiedene Ausnahmetypen.
            return FormatErgebnis.Unbekannt;
        }
    }

    private static bool IstERechnungAnhang(string? name) =>
        name is not null && ERechnungAnhaenge.Contains(Path.GetFileName(name), StringComparer.OrdinalIgnoreCase);

    private static bool SiehtAusWieXml(byte[] inhalt)
    {
        var start = BeginntMit(inhalt, [0xEF, 0xBB, 0xBF]) ? 3 : 0;
        for (var i = start; i < inhalt.Length; i++)
        {
            if (inhalt[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return inhalt[i] == (byte)'<';
        }

        return false;
    }

    private static bool IstERechnungXml(byte[] inhalt)
    {
        var einstellungen = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using var leser = XmlReader.Create(new MemoryStream(inhalt), einstellungen);
            leser.MoveToContent();
            return (leser.LocalName, leser.NamespaceURI) switch
            {
                ("CrossIndustryInvoice", NamensraumCii) => true,
                ("Invoice", NamensraumUblRechnung) => true,
                ("CreditNote", NamensraumUblGutschrift) => true,
                _ => false,
            };
        }
        catch (XmlException)
        {
            return false;
        }
    }

    /// <summary>Zählt die Image File Directories (IFD) einer TIFF-Datei; 0 bei kaputter Struktur.</summary>
    private static int TiffSeiten(byte[] inhalt)
    {
        var littleEndian = inhalt[0] == (byte)'I';
        long offset = Lese32(inhalt, 4, littleEndian);
        var seiten = 0;
        var besucht = new HashSet<long>();
        while (offset != 0)
        {
            if (offset + 2 > inhalt.Length || !besucht.Add(offset))
            {
                return 0;
            }

            var eintraege = Lese16(inhalt, (int)offset, littleEndian);
            var naechster = offset + 2 + (eintraege * 12L);
            if (naechster + 4 > inhalt.Length)
            {
                return 0;
            }

            seiten++;
            offset = Lese32(inhalt, (int)naechster, littleEndian);
        }

        return seiten;
    }

    private static int Lese16(byte[] b, int i, bool le) =>
        le ? b[i] | (b[i + 1] << 8) : (b[i] << 8) | b[i + 1];

    private static uint Lese32(byte[] b, int i, bool le) =>
        le
            ? (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24))
            : (uint)((b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3]);

    private static bool BeginntMit(byte[] inhalt, ReadOnlySpan<byte> signatur) => inhalt.AsSpan().StartsWith(signatur);
}
