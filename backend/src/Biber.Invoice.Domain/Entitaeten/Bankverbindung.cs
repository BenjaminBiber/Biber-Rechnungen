namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Bankverbindung eines Korrespondenten; der BIC ist optional.</summary>
public sealed record Bankverbindung(string Iban, string? Bic = null)
{
    /// <summary>„IBAN (BIC)“ – so steht die Bankverbindung im Änderungsprotokoll.</summary>
    public override string ToString() => string.IsNullOrWhiteSpace(Bic) ? Iban : $"{Iban} ({Bic})";
}
