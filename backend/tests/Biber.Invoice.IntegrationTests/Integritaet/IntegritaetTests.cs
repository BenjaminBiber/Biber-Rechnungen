using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Integritaet;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Integritaet;

[Collection("Api")]
public sealed class IntegritaetTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Jetzt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly string _unterordner = Guid.NewGuid().ToString("N");

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Jetzt);
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Unveraenderte_Dateien_ergeben_keine_Abweichung_und_eine_Ok_Meldung()
    {
        var beleg = Testdaten.Beleg();
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Original, "a.pdf", [1, 2, 3]));
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.EingebettetesXml, "a.xml", [4, 5]));
        await fabrik.SpeichernAsync(beleg);

        var lauf = (await PruefenAsync()).ShouldNotBeNull();

        lauf.AnzahlDateien.ShouldBe(2);
        lauf.AnzahlAbweichungen.ShouldBe(0);
        lauf.BeendetAm.ShouldNotBeNull();
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegDateien.ToListAsync()).ShouldAllBe(d => d.PruefErgebnis == true && d.LetztePruefungAm == Jetzt);
        var meldung = (await db.Benachrichtigungen.ToListAsync()).ShouldHaveSingleItem();
        meldung.Art.ShouldBe(BenachrichtigungArt.IntegritaetOk);
        meldung.Titel.ShouldBe("Integritätsprüfung ohne Befund");
        meldung.Text.ShouldBe("2 Dateien geprüft, alle Prüfsummen stimmen.");
    }

    [Fact]
    public async Task Geaendertes_Byte_wird_erkannt_gemeldet_und_protokolliert()
    {
        var beleg = Testdaten.Beleg();
        var datei = Datei(beleg.Id, DateiRolle.Original, "b.pdf", [1, 2, 3]);
        beleg.Dateien.Add(datei);
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Original, "c.pdf", [9]));
        await fabrik.SpeichernAsync(beleg);
        await File.WriteAllBytesAsync(Absolut(datei.Pfad), [1, 2, 4]);

        var lauf = (await PruefenAsync()).ShouldNotBeNull();

        lauf.AnzahlAbweichungen.ShouldBe(1);
        lauf.Abweichungen.ShouldBe([datei.Pfad]);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegDateien.SingleAsync(d => d.Id == datei.Id)).PruefErgebnis.ShouldBe(false);
        var meldung = (await db.Benachrichtigungen.ToListAsync()).ShouldHaveSingleItem();
        meldung.Art.ShouldBe(BenachrichtigungArt.Integritaetsfehler);
        meldung.Schweregrad.ShouldBe(Schweregrad.Fehler);
        meldung.NurAdmins.ShouldBeTrue();
        meldung.Titel.ShouldBe("Integritätsfehler in der Ablage");
        meldung.Text.ShouldBe($"1 Datei weicht von ihrer Prüfsumme ab: {datei.Pfad}");
        meldung.Link.ShouldBe("/einstellungen/integritaet");
        var eintrag = (await db.AuditEintraege.Where(e => e.Aktion == IntegritaetsPruefer.Aktion).ToListAsync())
            .ShouldHaveSingleItem();
        eintrag.Entitaet.ShouldBe(nameof(BelegDatei));
        eintrag.EntitaetId.ShouldBe(datei.Id.ToString());
        eintrag.BelegId.ShouldBe(beleg.Id);

        (await fabrik.ErzeugeClient(Rolle.Benutzer).GetStringAsync(ApiRouten.Benachrichtigungen.Liste))
            .ShouldNotContain("Integritätsfehler");
    }

    [Fact]
    public async Task Geloeschte_Datei_zaehlt_als_Abweichung()
    {
        var beleg = Testdaten.Beleg();
        var dateien = Enumerable.Range(0, 4)
            .Select(i => Datei(beleg.Id, DateiRolle.Original, $"d{i}.pdf", [(byte)i])).ToList();
        beleg.Dateien.AddRange(dateien);
        await fabrik.SpeichernAsync(beleg);
        foreach (var datei in dateien)
        {
            File.Delete(Absolut(datei.Pfad));
        }

        var lauf = (await PruefenAsync()).ShouldNotBeNull();

        lauf.AnzahlAbweichungen.ShouldBe(4);
        using var scope = fabrik.ErzeugeScope();
        var meldung = (await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Benachrichtigungen.ToListAsync())
            .ShouldHaveSingleItem();
        meldung.Text.ShouldStartWith("4 Dateien weichen von ihrer Prüfsumme ab: ");
        meldung.Text.Split(", ").Length.ShouldBe(3);
    }

    [Fact]
    public async Task Vorschau_Dateien_werden_nicht_geprueft()
    {
        var beleg = Testdaten.Beleg();
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Original, "e.pdf", [1]));
        var vorschau = Datei(beleg.Id, DateiRolle.Vorschau, "e.png", [2]);
        vorschau.Sha256 = "falsch";
        beleg.Dateien.Add(vorschau);
        await fabrik.SpeichernAsync(beleg);

        var lauf = (await PruefenAsync()).ShouldNotBeNull();

        lauf.AnzahlDateien.ShouldBe(1);
        lauf.AnzahlAbweichungen.ShouldBe(0);
        using var scope = fabrik.ErzeugeScope();
        (await scope.ServiceProvider.GetRequiredService<BiberDbContext>().BelegDateien.SingleAsync(d => d.Id == vorschau.Id))
            .PruefErgebnis.ShouldBeNull();
    }

    [Fact]
    public async Task Liste_der_Laeufe_neueste_zuerst()
    {
        await PruefenAsync();
        fabrik.Zeit.Vorspulen(TimeSpan.FromDays(1));
        await PruefenAsync();

        var laeufe = (await fabrik.ErzeugeClient(Rolle.NurLesen)
            .GetFromJsonAsync<IntegritaetsLaufDto[]>(ApiRouten.Integritaet.Laeufe)).ShouldNotBeNull();

        laeufe.Length.ShouldBe(2);
        laeufe[0].GestartetAm.ShouldBe(Jetzt.AddDays(1));
        laeufe[0].DauerSekunden.ShouldBe(0);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Manueller_Start_ohne_Admin_liefert_403(Rolle rolle)
    {
        var (api, starter) = MitZaehlStarter();

        (await api.ErzeugeClient(rolle).PostAsync(ApiRouten.Integritaet.Starten, null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        starter.Anzahl.ShouldBe(0);
    }

    [Fact]
    public async Task Manueller_Start_als_Admin_liefert_202_und_bei_laufender_Pruefung_409()
    {
        var (api, starter) = MitZaehlStarter();
        var client = api.ErzeugeClient();

        (await client.PostAsync(ApiRouten.Integritaet.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        starter.Anzahl.ShouldBe(1);

        await fabrik.SpeichernAsync(new IntegritaetsLauf { Id = Guid.NewGuid(), GestartetAm = Jetzt });
        (await client.PostAsync(ApiRouten.Integritaet.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        starter.Anzahl.ShouldBe(1);
        (await PruefenAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task Backup_Status_nennt_den_juengsten_von_drei_Dumps()
    {
        var ordner = BackupOrdnerLeeren();
        Dump(ordner, "a.dump", 10, Jetzt.AddDays(-2));
        Dump(ordner, "b.sql.gz", 20, Jetzt.AddHours(-8));
        Dump(ordner, "c.dump", 30, Jetzt.AddDays(-1));
        Dump(ordner, "notiz.txt", 40, Jetzt);

        var stand = (await fabrik.ErzeugeClient(Rolle.NurLesen)
            .GetFromJsonAsync<BackupStatusDto>(ApiRouten.SystemInfo.Backup)).ShouldNotBeNull();

        stand.LetztesBackup.ShouldBe(Jetzt.AddHours(-8));
        stand.Groesse.ShouldBe(20);
        stand.AnzahlDumps.ShouldBe(3);
    }

    [Fact]
    public async Task Backup_Status_bei_leerem_Ordner()
    {
        BackupOrdnerLeeren();

        var stand = (await fabrik.ErzeugeClient()
            .GetFromJsonAsync<BackupStatusDto>(ApiRouten.SystemInfo.Backup)).ShouldNotBeNull();

        stand.ShouldBe(new BackupStatusDto(null, null, 0));
    }

    [Fact]
    public async Task Status_Quelle_Integritaet_in_allen_drei_Zustaenden()
    {
        (await StatusAsync("integritaet")).Schweregrad.ShouldBe(Schweregrad.Info);

        var beleg = Testdaten.Beleg();
        var datei = Datei(beleg.Id, DateiRolle.Original, "f.pdf", [1]);
        beleg.Dateien.Add(datei);
        await fabrik.SpeichernAsync(beleg);
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 2, 0, 0, TimeSpan.Zero)); // 03:00 in Berlin
        await PruefenAsync();
        fabrik.Zeit.Setze(Jetzt);
        var ok = await StatusAsync("integritaet");
        ok.Schweregrad.ShouldBe(Schweregrad.Ok);
        ok.Titel.ShouldBe("Integritätsprüfung ohne Befund");
        ok.Text.ShouldBe("1 Dateien, SHA-256 geprüft · heute 03:00");

        File.Delete(Absolut(datei.Pfad));
        fabrik.Zeit.Vorspulen(TimeSpan.FromMinutes(1));
        await PruefenAsync();
        (await StatusAsync("integritaet")).Schweregrad.ShouldBe(Schweregrad.Fehler);
    }

    [Fact]
    public async Task Status_Quelle_Backup_in_allen_drei_Zustaenden()
    {
        var ordner = BackupOrdnerLeeren();
        (await StatusAsync("backup")).Titel.ShouldBe("Kein aktuelles Datenbank-Backup");

        Dump(ordner, "a.dump", 1, new DateTimeOffset(2026, 1, 15, 1, 0, 0, TimeSpan.Zero)); // 02:00 in Berlin
        var ok = await StatusAsync("backup");
        ok.Schweregrad.ShouldBe(Schweregrad.Ok);
        ok.Titel.ShouldBe("Datenbank-Backup erstellt");
        ok.Text.ShouldBe("pg_dump heute 02:00 · 1 Dumps vorhanden");

        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 16, 3, 1, 0, TimeSpan.Zero));
        var alt = await StatusAsync("backup");
        alt.Schweregrad.ShouldBe(Schweregrad.Warnung);
        alt.Titel.ShouldBe("Kein aktuelles Datenbank-Backup");
    }

    private async Task<IntegritaetsLauf?> PruefenAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<IntegritaetsPruefer>().PruefenAsync(CancellationToken.None);
    }

    private async Task<Contracts.Dto.Auswertung.SystemStatusEintragDto> StatusAsync(string schluessel)
    {
        using var scope = fabrik.ErzeugeScope();
        var quelle = scope.ServiceProvider.GetServices<ISystemStatusQuelle>().Single(q => q.Schluessel == schluessel);
        return await quelle.LesenAsync(CancellationToken.None);
    }

    private (ApiFabrik Api, ZaehlStarter Starter) MitZaehlStarter()
    {
        var starter = new ZaehlStarter();
        var api = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<IIntegritaetsStarter>();
            d.AddSingleton<IIntegritaetsStarter>(starter);
        });
        return (api, starter);
    }

    private string Absolut(string pfad) => Path.Combine(fabrik.TempOrdner, "ablage", pfad);

    private BelegDatei Datei(Guid belegId, DateiRolle rolle, string name, byte[] inhalt)
    {
        var pfad = Path.Combine(_unterordner, name);
        var absolut = Absolut(pfad);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        File.WriteAllBytes(absolut, inhalt);
        return new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = belegId,
            Rolle = rolle,
            Pfad = pfad,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(inhalt)),
            Groesse = inhalt.Length,
            MimeType = "application/octet-stream",
            AbgelegtAm = Jetzt,
        };
    }

    private string BackupOrdnerLeeren()
    {
        var ordner = Path.Combine(fabrik.TempOrdner, "backup");
        if (Directory.Exists(ordner))
        {
            Directory.Delete(ordner, recursive: true);
        }

        Directory.CreateDirectory(ordner);
        return ordner;
    }

    private static void Dump(string ordner, string name, int groesse, DateTimeOffset zeit)
    {
        var pfad = Path.Combine(ordner, name);
        File.WriteAllBytes(pfad, new byte[groesse]);
        File.SetLastWriteTimeUtc(pfad, zeit.UtcDateTime);
    }

    private sealed class ZaehlStarter : IIntegritaetsStarter
    {
        public int Anzahl { get; private set; }

        public void Starten() => Anzahl++;
    }
}
