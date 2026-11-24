using Biber.Invoice.Domain.Pipeline;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace Biber.Invoice.Infrastructure.Module.Aufteilen;

/// <summary>Teilt PDFs mit PdfPig; das Quell-PDF wird nur gelesen, die Seiten werden in ein neues PDF kopiert.</summary>
public sealed class PdfTeiler : IPdfTeiler
{
    public byte[] Extrahieren(Stream pdf, IReadOnlyList<int> seiten)
    {
        using var quelle = PdfDocument.Open(pdf);
        using var ziel = new PdfDocumentBuilder();
        foreach (var seite in seiten)
        {
            if (seite < 1 || seite > quelle.NumberOfPages)
            {
                throw new ArgumentOutOfRangeException(nameof(seiten), seite, $"Das PDF hat {quelle.NumberOfPages} Seiten.");
            }

            ziel.AddPage(quelle, seite);
        }

        return ziel.Build();
    }
}
