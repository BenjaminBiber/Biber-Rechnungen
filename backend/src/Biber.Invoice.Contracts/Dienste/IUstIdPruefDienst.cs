namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Prüft USt-IdNr. von Korrespondenten online über VIES.</summary>
public interface IUstIdPruefDienst
{
    /// <summary>Prüft sofort (auch wenn die regelmäßige Prüfung abgeschaltet ist) und speichert das Ergebnis.</summary>
    /// <returns><c>false</c>, wenn es den Korrespondenten nicht gibt.</returns>
    Task<bool> PruefenAsync(Guid korrespondentId, CancellationToken ct);
}
