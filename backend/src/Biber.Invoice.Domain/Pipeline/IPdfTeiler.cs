namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Erzeugt aus einem PDF ein neues PDF mit ausgewählten Seiten.</summary>
public interface IPdfTeiler
{
    /// <summary>Kopiert die <paramref name="seiten"/> (ab 1, in der angegebenen Reihenfolge) in ein neues PDF.</summary>
    byte[] Extrahieren(Stream pdf, IReadOnlyList<int> seiten);
}
