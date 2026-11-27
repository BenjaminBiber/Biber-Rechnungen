using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Module.Korrespondenten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>
/// Name der kontoführenden Bank zu einer deutschen IBAN: Die Stellen 5–12 sind die Bankleitzahl, ihr Name steht in
/// der Tabelle <see cref="Bank"/> (Bankleitzahlendatei der Bundesbank, siehe <see cref="BankleitzahlenDienst"/>).
/// </summary>
public static class BankVerzeichnis
{
    /// <summary>Bankverbindungen als DTO, jede mit dem Banknamen, soweit er sich ermitteln lässt.</summary>
    public static async Task<BankverbindungDto[]> DtosAsync(
        BiberDbContext db, IReadOnlyCollection<Bankverbindung> verbindungen, CancellationToken ct)
    {
        var bankleitzahlen = verbindungen.Select(v => Blz(v.Iban)).OfType<string>().Distinct().ToList();
        var namen = bankleitzahlen.Count == 0
            ? []
            : await db.Banken.AsNoTracking()
                .Where(b => bankleitzahlen.Contains(b.Blz))
                .ToDictionaryAsync(b => b.Blz, b => b.Name, ct);

        return
        [
            .. verbindungen.Select(v => new BankverbindungDto(
                v.Iban, v.Bic, Blz(v.Iban) is { } blz ? namen.GetValueOrDefault(blz) : null)),
        ];
    }

    /// <summary>Bankleitzahl einer gültigen deutschen IBAN; <c>null</c> bei ausländischer oder ungültiger IBAN.</summary>
    public static string? Blz(string? iban)
    {
        var wert = KorrespondentNormalisierung.Kennung(iban);
        return wert is not null && wert.StartsWith("DE", StringComparison.Ordinal) && wert.Length == 22
            && KorrespondentNormalisierung.IstIban(wert)
                ? wert.Substring(4, 8)
                : null;
    }
}
