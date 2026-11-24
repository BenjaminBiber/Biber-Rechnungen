using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>
/// Erbauer für Testdaten mit sinnvollen Standardwerten. Namen sind eindeutig, damit Tests sich nicht
/// über eindeutige Indizes in die Quere kommen. Speichern mit <see cref="SpeichernAsync{T}"/>.
/// </summary>
public static class Testdaten
{
    private static readonly DateTimeOffset Zeitpunkt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public static Beleg Beleg(
        string? rechnungsnummer = null,
        DateOnly? rechnungsdatum = null,
        decimal? brutto = 119.00m,
        Guid? korrespondentId = null,
        Guid? kategorieId = null,
        string? volltext = null,
        BelegStatus status = BelegStatus.Fertig) => new()
        {
            Id = Guid.NewGuid(),
            Richtung = Richtung.Eingang,
            Belegart = Belegart.Rechnung,
            Status = status,
            Quelle = ImportQuelle.Upload,
            KorrespondentId = korrespondentId,
            KorrespondentErkennung = korrespondentId is null ? KorrespondentErkennung.Keine : KorrespondentErkennung.Manuell,
            KategorieId = kategorieId,
            Rechnungsnummer = rechnungsnummer ?? $"RE-{Eindeutig()}",
            Rechnungsdatum = rechnungsdatum ?? new DateOnly(2026, 1, 10),
            Netto = brutto is null ? null : Math.Round(brutto.Value / 1.19m, 2),
            Steuer = brutto is null ? null : brutto - Math.Round(brutto.Value / 1.19m, 2),
            Brutto = brutto,
            ExtraktionsQuelle = ExtraktionsQuelle.Manuell,
            Seitenanzahl = 1,
            Volltext = volltext,
            ErstelltAm = Zeitpunkt,
        };

    public static Korrespondent Korrespondent(string? name = null, string? ustIdNr = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name ?? $"Lieferant {Eindeutig()}",
        UstIdNr = ustIdNr,
        IstLieferant = true,
        ErstelltAm = Zeitpunkt,
        Geprueft = true,
    };

    public static Kategorie Kategorie(string? name = null, string farbe = "#1F6FEB") => new()
    {
        Id = Guid.NewGuid(),
        Name = name ?? $"Kategorie {Eindeutig()}",
        Farbe = farbe,
    };

    public static Tag Tag(string? name = null, string farbe = "grau") => new()
    {
        Id = Guid.NewGuid(),
        Name = name ?? $"Tag {Eindeutig()}",
        Farbe = farbe,
    };

    public static Benutzer Benutzer(string? email = null, string anzeigename = "Test Benutzer", bool freigegeben = true)
    {
        email ??= $"benutzer-{Eindeutig()}@example.org";
        return new Benutzer
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Anzeigename = anzeigename,
            Freigegeben = freigegeben,
            ErstelltAm = Zeitpunkt,
        };
    }

    /// <summary>Speichert <paramref name="entitaet"/> in einem eigenen Scope und gibt sie zurück.</summary>
    public static async Task<T> SpeichernAsync<T>(this ApiFabrik fabrik, T entitaet)
        where T : class
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        db.Add(entitaet);
        await db.SaveChangesAsync();
        return entitaet;
    }

    private static string Eindeutig() => Guid.NewGuid().ToString("N")[..8];
}
