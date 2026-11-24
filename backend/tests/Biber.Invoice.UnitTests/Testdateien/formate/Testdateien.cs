using System.Text;
using SkiaSharp;

namespace Biber.Invoice.UnitTests.Testdateien.Formate;

/// <summary>
/// Erzeugt die Testdateien der Formaterkennung im Speicher, damit kein Binärmaterial im Repo liegt
/// und andere Tests sie wiederverwenden können.
/// </summary>
public static class Testdateien
{
    public static readonly string CiiXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <rsm:CrossIndustryInvoice xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100">
          <rsm:ExchangedDocument />
        </rsm:CrossIndustryInvoice>
        """;

    public static readonly string UblXml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">
          <ID>RE-1</ID>
        </Invoice>
        """;

    public static readonly string UblGutschriftXml =
        """
        <CreditNote xmlns="urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2"><ID>G-1</ID></CreditNote>
        """;

    public static readonly string FremdesXml = "<?xml version=\"1.0\"?><Bestellung xmlns=\"urn:beispiel\" />";

    public static byte[] Cii() => Utf8MitBom(CiiXml);

    public static byte[] Ubl() => Encoding.UTF8.GetBytes(UblXml);

    public static byte[] UblGutschrift() => Encoding.UTF8.GetBytes(UblGutschriftXml);

    public static byte[] AnderesXml() => Encoding.UTF8.GetBytes(FremdesXml);

    public static byte[] Pdf(int seiten = 1) => ErzeugePdf(seiten);

    /// <summary>PDF mit eingebettetem CII-XML unter <paramref name="anhang"/>.</summary>
    public static byte[] ZugferdPdf(string anhang = "factur-x.xml", int seiten = 1) =>
        ErzeugePdf(seiten, (anhang, Encoding.UTF8.GetBytes(CiiXml)));

    public static byte[] Png() => Bild(SKEncodedImageFormat.Png);

    public static byte[] Jpg() => Bild(SKEncodedImageFormat.Jpeg);

    /// <summary>Minimales Little-Endian-TIFF mit <paramref name="seiten"/> Frames zu je 1×1 Pixel.</summary>
    public static byte[] Tiff(int seiten = 3)
    {
        using var puffer = new MemoryStream();
        using var schreiber = new BinaryWriter(puffer);
        schreiber.Write("II*\0"u8);
        schreiber.Write(8u);
        (ushort Tag, ushort Typ, uint Wert)[] felder =
        [
            (256, 3, 1), // Breite
            (257, 3, 1), // Höhe
            (258, 3, 8), // Bits je Sample
            (259, 3, 1), // keine Kompression
            (262, 3, 1), // Schwarz ist null
            (273, 4, 0), // Streifen-Offset, unten gesetzt
            (277, 3, 1), // Samples je Pixel
            (278, 3, 1), // Zeilen je Streifen
            (279, 4, 1), // Streifenlänge
        ];
        const int ifdGroesse = 2 + (9 * 12) + 4;
        for (var i = 0; i < seiten; i++)
        {
            var start = 8 + (i * (ifdGroesse + 1));
            var pixel = (uint)(start + ifdGroesse);
            schreiber.Write((ushort)felder.Length);
            foreach (var (tag, typ, wert) in felder)
            {
                schreiber.Write(tag);
                schreiber.Write(typ);
                schreiber.Write(1u);
                schreiber.Write(tag == 273 ? pixel : wert);
            }

            schreiber.Write(i == seiten - 1 ? 0u : (uint)(pixel + 1));
            schreiber.Write((byte)0x80);
        }

        schreiber.Flush();
        return puffer.ToArray();
    }

    public static byte[] TextAlsPdf() => Encoding.UTF8.GetBytes("Das ist nur Text, auch wenn die Datei .pdf heißt.");

    /// <summary>Die erste Hälfte eines gültigen PDFs.</summary>
    public static byte[] AbgeschnittenesPdf()
    {
        var pdf = Pdf(2);
        return pdf[..(pdf.Length / 2)];
    }

    /// <summary>
    /// Schreibt ein minimales PDF von Hand (PDFsharp verlangt ab 6.5 eine Lizenz). Optional mit einer
    /// eingebetteten Datei im Namensbaum <c>/EmbeddedFiles</c>, wie ZUGFeRD sie verwendet.
    /// </summary>
    private static byte[] ErzeugePdf(int seiten, (string Name, byte[] Inhalt)? anhang = null)
    {
        var objekte = new List<string>();
        var ersteSeite = anhang is null ? 3 : 5;
        var kinder = string.Join(' ', Enumerable.Range(ersteSeite, seiten).Select(n => $"{n} 0 R"));
        var namen = anhang is { } a ? $" /Names << /EmbeddedFiles << /Names [({a.Name}) 3 0 R] >> >>" : "";
        objekte.Add($"<< /Type /Catalog /Pages 2 0 R{namen} >>");
        objekte.Add($"<< /Type /Pages /Kids [{kinder}] /Count {seiten} >>");
        if (anhang is { } b)
        {
            objekte.Add($"<< /Type /Filespec /F ({b.Name}) /UF ({b.Name}) /EF << /F 4 0 R >> >>");
            objekte.Add($"<< /Type /EmbeddedFile /Length {b.Inhalt.Length} >>\nstream\n{Encoding.Latin1.GetString(b.Inhalt)}\nendstream");
        }

        for (var i = 0; i < seiten; i++)
        {
            objekte.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>");
        }

        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objekte.Count; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objekte[i]}\nendobj\n");
        }

        var xref = text.Length;
        text.Append($"xref\n0 {objekte.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            text.Append($"{offset:D10} 00000 n \n");
        }

        text.Append($"trailer\n<< /Size {objekte.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    private static byte[] Bild(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.White);
        using var daten = bitmap.Encode(format, 90);
        return daten.ToArray();
    }

    private static byte[] Utf8MitBom(string text) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];
}
