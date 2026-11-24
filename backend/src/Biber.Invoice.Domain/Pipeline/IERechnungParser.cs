namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Liest die strukturierten Daten einer E-Rechnung (CII oder UBL) aus ihrem XML.</summary>
public interface IERechnungParser
{
    /// <exception cref="ERechnungFormatException">Das XML ist nicht lesbar oder keine E-Rechnung.</exception>
    ExtraktionsErgebnis Parsen(byte[] xml);
}
