using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen;

/// <summary>
/// Schritt 8: setzt die Inbox-Tags (R4-6, nicht bei „Erneut auslesen“), führt die Plausibilitätsprüfungen aus und
/// setzt bei einem fehlgeschlagenen Befund „zu prüfen“. Eine fehlgeschlagene KoSIT-Validierung wird zusätzlich gemeldet.
/// </summary>
public sealed class PruefungenSchritt(
    BiberDbContext db,
    IPruefDienst pruefDienst,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    TimeProvider zeit) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.Pruefungen;

    public string Name => "Prüfungen";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (!kontext.IstWiederholung)
        {
            await InboxTagsSetzenAsync(beleg, ct);
        }

        var befunde = await pruefDienst.PruefenAsync(beleg, kontext, ct);

        if (befunde.FirstOrDefault(f => f.Pruefung == PruefungArt.Kosit && f.Ergebnis == PruefErgebnis.Fehlgeschlagen) is { } kosit)
        {
            await benachrichtigungen.MeldenAsync(
                new NeueBenachrichtigung(
                    BenachrichtigungArt.KositFehler,
                    Schweregrad.Fehler,
                    "KoSIT-Validierung fehlgeschlagen",
                    $"{kontext.Dateiname}: {kosit.Detail}",
                    Link: $"/belege/{beleg.Id}/pruefung",
                    EntdoppelungsSchluessel: $"kosit:{beleg.Id}"),
                ct);
        }

        return SchrittErgebnis.Weiter;
    }

    private async Task InboxTagsSetzenAsync(Beleg beleg, CancellationToken ct)
    {
        var inbox = await db.Tags.Where(t => t.IstInbox).Select(t => t.Id).ToListAsync(ct);
        var vorhanden = await db.BelegTags.Where(t => t.BelegId == beleg.Id).Select(t => t.TagId).ToListAsync(ct);
        var jetzt = zeit.GetUtcNow();
        foreach (var tagId in inbox.Except(vorhanden))
        {
            db.BelegTags.Add(new BelegTag { BelegId = beleg.Id, TagId = tagId, GesetztAm = jetzt });
        }
    }
}
