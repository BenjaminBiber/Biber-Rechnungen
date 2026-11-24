using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen;

/// <summary>
/// Baut die <see cref="PruefEingabe"/> aus Beleg, Einstellungen und Datenbank, führt die Prüfungen aus und ersetzt
/// die Befunde (B3: „zu prüfen“ setzt nur die Pipeline, entfernt wird der Tag nur durch Bestätigen).
/// </summary>
public sealed class PruefDienst(
    BiberDbContext db,
    IEinstellungen einstellungen,
    IEnumerable<IPruefung> pruefungen,
    TimeProvider zeit) : IPruefDienst
{
    public async Task<IReadOnlyList<PruefBefund>> PruefenAsync(Beleg beleg, VerarbeitungsKontext? kontext, CancellationToken ct)
    {
        var eingabe = await EingabeAsync(beleg, kontext, ct);
        var jetzt = zeit.GetUtcNow();
        var alte = await db.PruefBefunde.Where(f => f.BelegId == beleg.Id).ToListAsync(ct);

        // Ohne Kontext fehlt das KoSIT-Ergebnis; der Befund aus der Pipeline bleibt dann stehen.
        var behalten = kontext is null ? alte.Where(f => f.Pruefung == PruefungArt.Kosit).ToList() : [];
        db.PruefBefunde.RemoveRange(alte.Except(behalten));

        var neue = PruefLauf.Ausfuehren(eingabe, pruefungen)
            .Where(b => !behalten.Any(f => f.Pruefung == b.Art))
            .Select(b => new PruefBefund
            {
                Id = Guid.NewGuid(),
                BelegId = beleg.Id,
                Pruefung = b.Art,
                Ergebnis = b.Wert.Ergebnis,
                Detail = b.Wert.Detail,
                Zeitpunkt = jetzt,
            })
            .ToList();
        db.PruefBefunde.AddRange(neue);

        if (kontext is not null && neue.Any(f => f.Ergebnis == PruefErgebnis.Fehlgeschlagen)
            && !await db.BelegTags.AnyAsync(t => t.BelegId == beleg.Id && t.TagId == SystemIds.TagZuPruefen, ct)
            && !db.BelegTags.Local.Any(t => t.BelegId == beleg.Id && t.TagId == SystemIds.TagZuPruefen))
        {
            db.BelegTags.Add(new BelegTag { BelegId = beleg.Id, TagId = SystemIds.TagZuPruefen, GesetztAm = jetzt });
        }

        return neue;
    }

    private async Task<PruefEingabe> EingabeAsync(Beleg beleg, VerarbeitungsKontext? kontext, CancellationToken ct)
    {
        // Neue Belege bringen ihre Zeilen mit; bei geladenen fehlen sie, wenn sie nicht mit Include geladen wurden.
        var entry = db.Entry(beleg);
        if (entry.State is EntityState.Unchanged or EntityState.Modified)
        {
            if (!entry.Collection(b => b.Steuerzeilen).IsLoaded)
            {
                await entry.Collection(b => b.Steuerzeilen).LoadAsync(ct);
            }

            if (!entry.Collection(b => b.Positionen).IsLoaded)
            {
                await entry.Collection(b => b.Positionen).LoadAsync(ct);
            }
        }

        var pruef = await einstellungen.LesenAsync<PruefEinstellungen>(ct);
        var ust = await einstellungen.LesenAsync<UstEinstellungen>(ct);
        var allgemein = await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct);

        Korrespondent? korrespondent = null;
        var belege = 0;
        if (beleg.KorrespondentId is { } korrespondentId)
        {
            korrespondent = beleg.Korrespondent?.Id == korrespondentId
                ? beleg.Korrespondent
                : await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
            belege = await db.Belege.CountAsync(b => b.KorrespondentId == korrespondentId && b.Id != beleg.Id, ct) + 1;
        }

        string? duplikatVon = null;
        if (beleg.DuplikatVonId is { } duplikatVonId)
        {
            var nummer = await db.Belege.Where(b => b.Id == duplikatVonId).Select(b => b.Rechnungsnummer).FirstOrDefaultAsync(ct);
            duplikatVon = string.IsNullOrWhiteSpace(nummer) ? "einem Beleg ohne Rechnungsnummer" : nummer;
        }

        return new PruefEingabe(
            beleg,
            pruef,
            ust,
            Heute(allgemein.Zeitzone),
            KorrespondentIstNeu: korrespondent is { Geprueft: false },
            KorrespondentBelege: belege,
            DuplikatVonNummer: duplikatVon,
            Kosit: kontext?.Kosit,
            Empfaenger: kontext?.Extraktion?.Empfaenger,
            Korrespondent: korrespondent);
    }

    private DateOnly Heute(string zeitzone)
    {
        var jetzt = zeit.GetUtcNow();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(zeitzone, out var zone))
        {
            jetzt = TimeZoneInfo.ConvertTime(jetzt, zone);
        }

        return DateOnly.FromDateTime(jetzt.DateTime);
    }
}
