using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Module.Einstellungen;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>Ergebnis des letzten Abrufs; <see cref="Fehler"/> ist <c>null</c>, wenn er gelang.</summary>
public sealed record MailAbrufStatus(DateTimeOffset? LetzterAbruf, string? Fehler, int Mails, int Anhaenge, int OhneBeleg);

/// <summary>Liest und schreibt den <see cref="MailAbrufStatus"/> in der <see cref="Einstellung"/> <c>mail.status</c>.</summary>
public static class MailStatusSpeicher
{
    public const string Schluessel = "mail.status";

    public static async Task<MailAbrufStatus?> LesenAsync(BiberDbContext db, CancellationToken ct)
    {
        var eintrag = await db.Einstellungen.AsNoTracking().SingleOrDefaultAsync(e => e.Schluessel == Schluessel, ct);
        return eintrag is null ? null : JsonSerializer.Deserialize<MailAbrufStatus>(eintrag.Wert, EinstellungenDienst.Json);
    }

    public static async Task SchreibenAsync(BiberDbContext db, MailAbrufStatus status, DateTimeOffset jetzt, CancellationToken ct)
    {
        var eintrag = await db.Einstellungen.SingleOrDefaultAsync(e => e.Schluessel == Schluessel, ct);
        if (eintrag is null)
        {
            eintrag = new Einstellung { Schluessel = Schluessel };
            db.Einstellungen.Add(eintrag);
        }

        eintrag.Wert = JsonSerializer.Serialize(status, EinstellungenDienst.Json);
        eintrag.GeaendertAm = jetzt;
        await db.SaveChangesAsync(ct);
    }
}
