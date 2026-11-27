using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>
/// Füllt die Tabelle <see cref="Bank"/> aus der Bankleitzahlendatei der Bundesbank – automatisch abgerufen
/// (<see cref="BankleitzahlenJob"/>, Einstellungen „Jetzt abrufen“) oder von einem Admin hochgeladen – und führt
/// den <see cref="BankleitzahlenStand"/>.
/// </summary>
public sealed class BankleitzahlenDienst(
    BiberDbContext db,
    IBundesbankClient bundesbank,
    TimeProvider zeit,
    ILogger<BankleitzahlenDienst> log)
{
    /// <summary>Höchstgröße einer hochgeladenen Datei; die echte ist gut 2 MB groß.</summary>
    public const long MaxUploadGroesse = 10 * 1024 * 1024;

    public async Task<BankleitzahlenStandDto> StandAsync(CancellationToken ct) =>
        AlsDto(await db.BankleitzahlenStaende.AsNoTracking().SingleOrDefaultAsync(ct) ?? new BankleitzahlenStand());

    /// <summary>Ersetzt die Bankleitzahlen durch eine hochgeladene Datei; eine falsche Datei lässt sie unverändert.</summary>
    /// <exception cref="BankleitzahlenAusnahme">Keine (vollständige) Bankleitzahlendatei im TXT-Format.</exception>
    public async Task<BankleitzahlenStandDto> HochladenAsync(byte[] inhalt, string dateiname, CancellationToken ct)
    {
        var banken = BankleitzahlenDatei.Lesen(inhalt);
        var stand = await StandZumAendernAsync(ct);
        await ErsetzenAsync(stand, banken, BankleitzahlenQuelle.Upload, Path.GetFileName(dateiname), ct);
        log.LogInformation("Bankleitzahlen aus hochgeladener Datei {Datei} übernommen: {Anzahl} Banken", stand.Datei, banken.Count);
        return AlsDto(stand);
    }

    /// <summary>
    /// Holt die aktuelle Datei von der Bundesbank. Ohne <paramref name="erzwingen"/> wird nur geladen, wenn ihre Adresse
    /// – und damit ihr Inhalt – sich seit dem letzten Laden geändert hat. Ein Fehler steht danach im Stand.
    /// </summary>
    public async Task<BankleitzahlenStandDto> AbrufenAsync(bool erzwingen, CancellationToken ct)
    {
        var stand = await StandZumAendernAsync(ct);
        stand.LetzterAbrufAm = zeit.GetUtcNow();
        try
        {
            var adresse = await bundesbank.DateiAdresseAsync(ct);
            if (!erzwingen && stand.Anzahl > 0 && stand.Datei == adresse.ToString())
            {
                stand.LetzterFehler = null;
                await db.SaveChangesAsync(ct);
                return AlsDto(stand);
            }

            var banken = BankleitzahlenDatei.Lesen(await bundesbank.LadenAsync(adresse, ct));
            await ErsetzenAsync(stand, banken, BankleitzahlenQuelle.Bundesbank, adresse.ToString(), ct);
            log.LogInformation("Bankleitzahlen von der Bundesbank übernommen: {Anzahl} Banken aus {Adresse}", banken.Count, adresse);
        }
        catch (BankleitzahlenAusnahme ex)
        {
            log.LogWarning(ex, "Bankleitzahlen ließen sich nicht von der Bundesbank holen: {Meldung}", ex.Message);
            stand.LetzterFehler = ex.Message.Length > BankleitzahlenStandKonfiguration.MaxFehlerLaenge
                ? ex.Message[..BankleitzahlenStandKonfiguration.MaxFehlerLaenge]
                : ex.Message;
            await db.SaveChangesAsync(ct);
        }

        return AlsDto(stand);
    }

    private async Task<BankleitzahlenStand> StandZumAendernAsync(CancellationToken ct)
    {
        var stand = await db.BankleitzahlenStaende.SingleOrDefaultAsync(ct);
        if (stand is null)
        {
            stand = new BankleitzahlenStand();
            db.BankleitzahlenStaende.Add(stand);
        }

        return stand;
    }

    /// <summary>Tauscht alle Banken in einer Transaktion aus, damit Abfragen nie eine halbe Tabelle sehen.</summary>
    private async Task ErsetzenAsync(
        BankleitzahlenStand stand, IReadOnlyList<Bank> banken, BankleitzahlenQuelle quelle, string datei, CancellationToken ct)
    {
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        await db.Banken.ExecuteDeleteAsync(ct);
        db.Banken.AddRange(banken);
        stand.Anzahl = banken.Count;
        stand.GeladenAm = zeit.GetUtcNow();
        stand.Quelle = quelle;
        stand.Datei = datei;
        if (quelle == BankleitzahlenQuelle.Bundesbank)
        {
            stand.LetzterFehler = null;
        }

        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
        db.ChangeTracker.Clear(); // die gut 3 500 Banken nicht weiter verfolgen
    }

    private static BankleitzahlenStandDto AlsDto(BankleitzahlenStand s) =>
        new(s.Anzahl, s.GeladenAm, s.Quelle, s.Datei, s.LetzterAbrufAm, s.LetzterFehler);
}
