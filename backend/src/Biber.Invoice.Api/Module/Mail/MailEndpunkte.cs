using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Mail;
using Biber.Invoice.Infrastructure.Persistenz;

namespace Biber.Invoice.Api.Module.Mail;

/// <summary>Status (Lesen), Verbindungstest und Abruf auf Knopfdruck (Admin) für den E-Mail-Import.</summary>
public sealed class MailEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Einstellungen.MailStatus, StatusAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Einstellungen.MailVerbindungTesten, (MailAbruf abruf, CancellationToken ct) => abruf.VerbindungTestenAsync(ct))
            .RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Einstellungen.MailJetztAbrufen, JetztAbrufenAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<MailStatusDto> StatusAsync(MailAbruf abruf, BiberDbContext db, CancellationToken ct)
    {
        var status = await MailStatusSpeicher.LesenAsync(db, ct);
        return new MailStatusDto(
            Verbunden: abruf.Konfiguriert && status is { LetzterAbruf: not null, Fehler: null },
            LetzterAbruf: status?.LetzterAbruf,
            Fehler: status?.Fehler);
    }

    private static async Task<AktionErgebnisDto> JetztAbrufenAsync(MailAbruf abruf, CancellationToken ct)
    {
        if (!abruf.Konfiguriert)
        {
            return new AktionErgebnisDto(false, MailAbruf.NichtEingerichtet);
        }

        try
        {
            var e = await abruf.AbrufenAsync(ct);
            return new AktionErgebnisDto(
                true,
                $"{MailAbruf.Anzahl(e.Mails, "Mail", "Mails")}, {MailAbruf.Anzahl(e.Anhaenge, "Anhang", "Anhänge")} übernommen");
        }
        catch (MailAbrufFehler fehler)
        {
            return new AktionErgebnisDto(false, fehler.Message);
        }
    }
}
