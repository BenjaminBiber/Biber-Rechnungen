using System.Text.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Einstellungen;
using Biber.Invoice.Infrastructure.Module.Mail;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Api.Module.Einstellungen;

/// <summary><c>GET /api/einstellungen</c> (Lesen) und <c>PUT /api/einstellungen/{gruppe}</c> (Admin).</summary>
public sealed class EinstellungenEndpunkte : IEndpunktModul
{
    private const string DiModell = "prebuilt-invoice";

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Einstellungen.Alle, AlleLesenAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPut(ApiRouten.Einstellungen.Gruppe, GruppeSpeichernAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> AlleLesenAsync(
        IEinstellungen einstellungen,
        BiberDbContext db,
        IConfiguration konfiguration,
        ImapZugangQuelle imap,
        CancellationToken ct)
    {
        var allgemein = await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct);
        var inboxTagId = await db.Tags.Where(t => t.IstInbox).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);

        var dto = new EinstellungenDto(
            allgemein with { InboxTagId = inboxTagId ?? Guid.Empty },
            await einstellungen.LesenAsync<UstEinstellungen>(ct),
            await einstellungen.LesenAsync<PruefEinstellungen>(ct),
            await einstellungen.LesenAsync<DiEinstellungen>(ct),
            await einstellungen.LesenAsync<MailEinstellungen>(ct),
            await einstellungen.LesenAsync<OrdnerEinstellungen>(ct),
            await einstellungen.LesenAsync<AufbewahrungEinstellungen>(ct),
            await einstellungen.LesenAsync<ZugriffEinstellungen>(ct),
            await imap.BeschreibenAsync(einstellungen, ct),
            Umgebung(konfiguration));
        return Results.Json(dto, EinstellungenDienst.Json);
    }

    /// <summary>Werte aus der Konfiguration – Schlüssel und Passwörter werden nur auf „gesetzt“ geprüft.</summary>
    private static UmgebungDto Umgebung(IConfiguration k)
    {
        static string? Wert(IConfiguration k, string schluessel) =>
            string.IsNullOrWhiteSpace(k[schluessel]) ? null : k[schluessel];

        return new UmgebungDto(
            AblagePfad: k["Pfade:Ablage"] ?? "",
            EingangPfad: k["Pfade:Eingang"] ?? "",
            OeffentlicheUrl: Wert(k, "Anwendung:OeffentlicheUrl"),
            DiEndpunkt: Wert(k, "DocumentIntelligence:Endpunkt"),
            DiModell: DiModell,
            DiKonfiguriert: Wert(k, "DocumentIntelligence:Endpunkt") is not null && Wert(k, "DocumentIntelligence:Schluessel") is not null,
            GoogleKonfiguriert: Wert(k, "Google:ClientId") is not null && Wert(k, "Google:ClientSecret") is not null);
    }

    private static async Task<IResult> GruppeSpeichernAsync(
        string gruppe,
        HttpRequest anfrage,
        IEinstellungen einstellungen,
        BiberDbContext db,
        CancellationToken ct)
    {
        var typ = gruppe switch
        {
            "allgemein" => typeof(AllgemeinEinstellungen),
            "ust" => typeof(UstEinstellungen),
            "pruefungen" => typeof(PruefEinstellungen),
            "di" => typeof(DiEinstellungen),
            "mail" => typeof(MailEinstellungen),
            "ordner" => typeof(OrdnerEinstellungen),
            "aufbewahrung" => typeof(AufbewahrungEinstellungen),
            "zugriff" => typeof(ZugriffEinstellungen),
            _ => null,
        };
        if (typ is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Unbekannte Einstellungsgruppe „{gruppe}“.");
        }

        object? wert;
        try
        {
            wert = await JsonSerializer.DeserializeAsync(anfrage.Body, typ, EinstellungenDienst.Json, ct);
        }
        catch (JsonException)
        {
            wert = null;
        }

        if (wert is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [""] = ["Ungültiger Inhalt."] });
        }

        var fehler = new Dictionary<string, string[]>();
        Pruefen(wert, fehler);

        if (wert is AllgemeinEinstellungen { InboxTagId: var inboxTagId } && inboxTagId != Guid.Empty)
        {
            var tag = await db.Tags.SingleOrDefaultAsync(t => t.Id == inboxTagId, ct);
            if (tag is null || tag.IstSystem)
            {
                fehler["inboxTagId"] = [tag is null ? "Tag nicht gefunden." : "System-Tags sind nicht wählbar."];
            }
            else if (fehler.Count == 0)
            {
                await db.Tags.Where(t => t.IstInbox != (t.Id == inboxTagId))
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IstInbox, t => t.Id == inboxTagId), ct);
            }
        }

        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        await einstellungen.SpeichernAsync(wert, ct);
        if (wert is MailEinstellungen)
        {
            // Ein Fehler aus der Zeit vor der Änderung (oder vor dem Aktivieren) ist überholt; der Job ruft gleich neu ab.
            await MailStatusSpeicher.FehlerVerwerfenAsync(db, ct);
        }

        return Results.NoContent();
    }

    private static void Pruefen(object wert, Dictionary<string, string[]> fehler)
    {
        void Bereich(string feld, decimal ist, decimal min, decimal max)
        {
            if (ist < min || ist > max)
            {
                fehler[feld] = [$"Erlaubt ist {min} bis {max}."];
            }
        }

        void NichtLeer(string feld, string? ist)
        {
            if (string.IsNullOrWhiteSpace(ist))
            {
                fehler[feld] = ["Darf nicht leer sein."];
            }
        }

        switch (wert)
        {
            case AllgemeinEinstellungen a:
                if (a.Firmenname is { Length: > 200 })
                {
                    fehler["firmenname"] = ["Höchstens 200 Zeichen."];
                }

                break;
            case PruefEinstellungen p:
                Bereich("summenToleranz", p.SummenToleranz, 0, 10);
                Bereich("maxAlterJahre", p.MaxAlterJahre, 1, 30);
                Bereich("kleinbetragGrenze", p.KleinbetragGrenze, 0, 1000);
                Bereich("konfidenzSchwelle", p.KonfidenzSchwelle, 0, 1);
                break;
            case DiEinstellungen d:
                Bereich("monatslimit", d.Monatslimit, 0, int.MaxValue);
                Bereich("warnungAbProzent", d.WarnungAbProzent, 1, 100);
                Bereich("maxSeitenJeBeleg", d.MaxSeitenJeBeleg, 1, 500);
                break;
            case MailEinstellungen m:
                Bereich("abrufintervallMinuten", m.AbrufintervallMinuten, 1, 1440);
                NichtLeer("eingangsordner", m.Eingangsordner);
                NichtLeer("verarbeitetOrdner", m.VerarbeitetOrdner);
                NichtLeer("ohneBelegOrdner", m.OhneBelegOrdner);
                break;
            case OrdnerEinstellungen o:
                Bereich("pollingSekunden", o.PollingSekunden, 5, 3600);
                Bereich("stabilSekunden", o.StabilSekunden, 1, 600);
                break;
            case AufbewahrungEinstellungen au:
                Bereich("jahre", au.Jahre, 1, 30);
                break;
        }
    }
}
