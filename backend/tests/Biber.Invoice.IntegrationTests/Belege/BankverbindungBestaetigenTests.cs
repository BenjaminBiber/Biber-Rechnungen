using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Belege;

/// <summary>Eine abweichende IBAN wird gemeldet und erst mit dem Bestätigen des Belegs beim Korrespondenten hinterlegt.</summary>
[Collection("Api")]
public sealed class BankverbindungBestaetigenTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string Bekannt = "DE89370400440532013000";
    private const string Neu = "DE02120300000000202051";

    public async Task InitializeAsync()
    {
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            var benutzer = Testdaten.Benutzer();
            benutzer.Id = ApiFabrik.BenutzerId(rolle);
            await fabrik.SpeichernAsync(benutzer);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Abweichende_IBAN_wird_gemeldet_und_erst_beim_Bestaetigen_uebernommen()
    {
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.Geprueft = true;
        korrespondent.Bankverbindungen = [new Bankverbindung(Bekannt)];
        await fabrik.SpeichernAsync(korrespondent);
        var beleg = await BelegAsync(korrespondent.Id, [new Bankverbindung(Neu, "BYLADEM1001")]);

        (await PruefenAsync(beleg.Id)).Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
        (await KorrespondentAsync(korrespondent.Id)).Bankverbindungen.ShouldBe([new Bankverbindung(Bekannt)]);

        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).PostJsonAsync(
            ApiRouten.Belege.Bestaetigen.Replace("{id}", beleg.Id.ToString(), StringComparison.Ordinal), new BestaetigenDto("Neue Bank laut Telefonat"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await KorrespondentAsync(korrespondent.Id)).Bankverbindungen
            .ShouldBe([new Bankverbindung(Bekannt), new Bankverbindung(Neu, "BYLADEM1001")]);
        (await PruefenAsync(beleg.Id)).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Fact]
    public async Task IBAN_mit_falscher_Pruefziffer_wird_beim_Bestaetigen_nicht_uebernommen()
    {
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.Bankverbindungen = [new Bankverbindung(Bekannt)];
        await fabrik.SpeichernAsync(korrespondent);
        var beleg = await BelegAsync(korrespondent.Id, [new Bankverbindung("DE89370400440532013001")]);

        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).PostJsonAsync(
            ApiRouten.Belege.Bestaetigen.Replace("{id}", beleg.Id.ToString(), StringComparison.Ordinal), new BestaetigenDto(null));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await KorrespondentAsync(korrespondent.Id)).Bankverbindungen.ShouldBe([new Bankverbindung(Bekannt)]);
    }

    private async Task<Beleg> BelegAsync(Guid korrespondentId, List<Bankverbindung> bankverbindungen)
    {
        var beleg = Testdaten.Beleg(korrespondentId: korrespondentId);
        beleg.Bankverbindungen = bankverbindungen;
        beleg.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = fabrik.Zeit.GetUtcNow() }];
        return await fabrik.SpeichernAsync(beleg);
    }

    /// <summary>Prüft erneut wie nach einer Korrektur und liefert den Befund „Bankverbindung“.</summary>
    private async Task<PruefBefund> PruefenAsync(Guid belegId)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var beleg = await db.Belege.SingleAsync(b => b.Id == belegId);
        var befunde = await scope.ServiceProvider.GetRequiredService<IPruefDienst>().PruefenAsync(beleg, null, CancellationToken.None);
        await db.SaveChangesAsync();
        return befunde.Single(b => b.Pruefung == PruefungArt.Bankverbindung);
    }

    private async Task<Korrespondent> KorrespondentAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == id);
    }
}
