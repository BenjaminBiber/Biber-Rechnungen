namespace Biber.Invoice.Domain.Pipeline;

/// <param name="Pfad">Absoluter Pfad im Staging.</param>
/// <param name="Sha256">Hash als Kleinbuchstaben-Hex (64 Zeichen).</param>
public sealed record StagingDatei(string Pfad, string Sha256, long Groesse);
