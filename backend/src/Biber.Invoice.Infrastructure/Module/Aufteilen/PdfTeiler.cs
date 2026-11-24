using Biber.Invoice.Domain.Pipeline;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace Biber.Invoice.Infrastructure.Module.Aufteilen;

/// <summary>Teilt PDFs mit PDFsharp; das Quell-PDF wird nur im Import-Modus gelesen.</summary>
public sealed class PdfTeiler : IPdfTeiler
{
    public byte[] Extrahieren(Stream pdf, IReadOnlyList<int> seiten)
    {
        using var quelle = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
        using var ziel = new PdfDocument();
        foreach (var seite in seiten)
        {
            if (seite < 1 || seite > quelle.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(seiten), seite, $"Das PDF hat {quelle.PageCount} Seiten.");
            }

            ziel.AddPage(quelle.Pages[seite - 1]);
        }

        using var ausgabe = new MemoryStream();
        ziel.Save(ausgabe, false);
        return ausgabe.ToArray();
    }
}
