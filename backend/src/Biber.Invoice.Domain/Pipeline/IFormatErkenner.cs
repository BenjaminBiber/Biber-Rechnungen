using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Erkennt das Format einer eingegangenen Datei am Inhalt, nicht an der Endung.</summary>
public interface IFormatErkenner
{
    FormatErgebnis Erkennen(Stream datei, string dateiname);
}

/// <param name="ERechnungXml">XML der E-Rechnung (XRechnung-Datei oder in ZUGFeRD/Factur-X eingebettet).</param>
public sealed record FormatErgebnis(ErkanntesFormat Format, byte[]? ERechnungXml, int Seitenanzahl)
{
    public static FormatErgebnis Unbekannt { get; } = new(ErkanntesFormat.Unbekannt, null, 0);
}
