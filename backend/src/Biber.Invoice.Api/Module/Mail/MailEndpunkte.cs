using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Mail;
using Biber.Invoice.Infrastructure.Persistenz;

namespace Biber.Invoice.Api.Module.Mail;

/// <summary>Status (Lesen), Verbindungstest, Abruf auf Knopfdruck und IMAP-Zugang (Admin) für den E-Mail-Import.</summary>
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
        app.MapPut(ApiRouten.Einstellungen.MailZugang, ZugangSpeichernAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> ZugangSpeichernAsync(
        MailZugangSpeichernDto zugang,
        ImapZugangQuelle quelle,
        IEinstellungen einstellungen,
        BiberDbContext db,
        CancellationToken ct)
    {
        if (quelle.AusUmgebung)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Der IMAP-Zugang ist per .env gesetzt und lässt sich hier nicht ändern.");
        }

        var fehler = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(zugang.Host))
        {
            fehler["host"] = ["Darf nicht leer sein."];
        }
        else if (zugang.Host.Trim().Length > 255 || zugang.Host.Trim().Any(char.IsWhiteSpace))
        {
            fehler["host"] = ["Kein gültiger Servername."];
        }

        if (zugang.Port is < 1 or > 65535)
        {
            fehler["port"] = ["Erlaubt ist 1 bis 65535."];
        }

        if (string.IsNullOrWhiteSpace(zugang.Benutzer))
        {
            fehler["benutzer"] = ["Darf nicht leer sein."];
        }

        if (string.IsNullOrEmpty(zugang.Passwort) && !await quelle.PasswortGespeichertAsync(einstellungen, ct))
        {
            fehler["passwort"] = ["Darf nicht leer sein."];
        }

        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        await quelle.SpeichernAsync(einstellungen, zugang, ct);
        await MailStatusSpeicher.FehlerVerwerfenAsync(db, ct); // Ein Fehler mit dem alten Zugang ist überholt.
        return Results.NoContent();
    }

    private static async Task<MailStatusDto> StatusAsync(MailAbruf abruf, BiberDbContext db, CancellationToken ct)
    {
        var status = await MailStatusSpeicher.LesenAsync(db, ct);
        return new MailStatusDto(
            Verbunden: await abruf.KonfiguriertAsync(ct) && status is { LetzterAbruf: not null, Fehler: null },
            LetzterAbruf: status?.LetzterAbruf,
            Fehler: status?.Fehler);
    }

    private static async Task<AktionErgebnisDto> JetztAbrufenAsync(MailAbruf abruf, CancellationToken ct)
    {
        if (!await abruf.KonfiguriertAsync(ct))
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
