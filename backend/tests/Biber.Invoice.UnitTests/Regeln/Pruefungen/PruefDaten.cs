using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

/// <summary>Fehlerfreier Beleg wie im Design (Bürowelt Krämer, 264,00 €) als Ausgangspunkt der Prüfungstests.</summary>
internal static class PruefDaten
{
    public static readonly DateOnly Heute = new(2026, 11, 24);

    public static Korrespondent Korrespondent() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Bürowelt Krämer KG",
        UstIdNr = "DE284117560",
        Anschrift = new Anschrift { Strasse = "Hafenstraße 12", Plz = "20457", Ort = "Hamburg" },
        ErstelltAm = new DateTimeOffset(2024, 3, 5, 9, 0, 0, TimeSpan.Zero),
        Geprueft = true,
    };

    public static Beleg Beleg()
    {
        var korrespondent = Korrespondent();
        return new Beleg
        {
            Id = Guid.NewGuid(),
            KorrespondentId = korrespondent.Id,
            Korrespondent = korrespondent,
            Rechnungsnummer = "2026-48817",
            Rechnungsdatum = new DateOnly(2026, 11, 19),
            Leistungsdatum = new DateOnly(2026, 11, 19),
            Netto = 221.85m,
            Steuer = 42.15m,
            Brutto = 264.00m,
            ExtraktionsQuelle = ExtraktionsQuelle.DocumentIntelligence,
            DiKonfidenz = 0.93m,
            Steuerzeilen = [new Steuerzeile { Satz = 19m, Netto = 221.85m, Steuer = 42.15m }],
            Positionen =
            [
                Position(1, 117.70m),
                Position(2, 104.15m),
            ],
        };
    }

    public static Position Position(int nr, decimal? gesamt, decimal? steuersatz = 19m) =>
        new() { Nr = nr, Text = $"Position {nr}", Gesamt = gesamt, Steuersatz = steuersatz };

    public static PruefEingabe Eingabe(Beleg? beleg = null, PruefEinstellungen? einstellungen = null, UstEinstellungen? ust = null)
    {
        beleg ??= Beleg();
        return new PruefEingabe(
            beleg,
            einstellungen ?? new PruefEinstellungen(),
            ust ?? new UstEinstellungen { Status = UstStatus.Regelbesteuerung },
            Heute,
            Korrespondent: beleg.Korrespondent,
            KorrespondentIstNeu: beleg.Korrespondent is { Geprueft: false },
            KorrespondentBelege: beleg.Korrespondent is null ? 0 : 31);
    }
}
