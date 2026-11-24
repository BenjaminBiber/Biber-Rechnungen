using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Mail;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace Biber.Invoice.IntegrationTests.Mail;

[Collection("Api")]
public sealed class MailImportTests(ApiFabrik fabrik, GreenMail greenMail) : IClassFixture<GreenMail>, IAsyncLifetime
{
    private const string Verarbeitet = "Verarbeitet";
    private const string OhneBeleg = "Ohne Beleg";

    private static readonly byte[] Pdf = [.. "%PDF-1.7\n"u8, .. new byte[2000]];

    private readonly ImportProtokoll _protokoll = new();
    private ApiFabrik? _mail;

    private ApiFabrik Mail => _mail ??= MitPostfach(GreenMail.Passwort);

    private MailAbruf Abruf => Mail.Services.GetRequiredService<MailAbruf>();

    public async Task InitializeAsync()
    {
        await fabrik.ZuruecksetzenAsync();
        await greenMail.ZuruecksetzenAsync();
    }

    public async Task DisposeAsync()
    {
        if (_mail is not null)
        {
            await _mail.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mail_mit_PDF_wird_mit_Absender_Betreff_und_Message_Id_angenommen_und_verschoben()
    {
        await greenMail.SendenAsync(Nachricht("<rechnung-1@stadtwerke.test>", "Ihre Rechnung 2026-1194", ("rechnung.pdf", Pdf)));

        var ergebnis = await Abruf.AbrufenAsync(default);

        ergebnis.ShouldBe(new MailAbrufErgebnis(1, 1, 0));
        var aufruf = _protokoll.Aufrufe.ShouldHaveSingleItem();
        aufruf.Dateiname.ShouldBe("rechnung.pdf");
        aufruf.Quelle.ShouldBe(ImportQuelle.Mail);
        aufruf.Groesse.ShouldBe(Pdf.Length);
        aufruf.Herkunft.ShouldBe(new ImportHerkunft(
            MailMessageId: "rechnung-1@stadtwerke.test",
            MailAbsender: "rechnung@stadtwerke.test",
            MailBetreff: "Ihre Rechnung 2026-1194"));
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(0);
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(1);
        (await greenMail.AnzahlAsync(OhneBeleg)).ShouldBe(0);
    }

    [Fact]
    public async Task Mail_mit_zwei_Anhaengen_ergibt_zwei_Aufrufe()
    {
        await greenMail.SendenAsync(Nachricht("<zwei@lieferant.test>", "Rechnung und XRechnung", ("rechnung.pdf", Pdf), ("xrechnung.xml", "<Invoice/>"u8.ToArray())));

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(1, 2, 0));

        _protokoll.Aufrufe.Select(a => a.Dateiname).Order().ShouldBe(["rechnung.pdf", "xrechnung.xml"]);
        _protokoll.Aufrufe.ShouldAllBe(a => a.Herkunft.MailMessageId == "zwei@lieferant.test");
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(1);
    }

    [Fact]
    public async Task Newsletter_ohne_Anhang_wird_Vorgang_Ohne_Beleg_und_landet_im_Ordner_Ohne_Beleg()
    {
        await greenMail.SendenAsync(Nachricht("<news-7@shop.test>", "Unser Newsletter"));

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(1, 0, 1));

        _protokoll.Aufrufe.ShouldBeEmpty();
        var vorgang = (await VorgaengeAsync()).ShouldHaveSingleItem();
        vorgang.Status.ShouldBe(ImportStatus.OhneBeleg);
        vorgang.Quelle.ShouldBe(ImportQuelle.Mail);
        vorgang.Ergebnis.ShouldBe("Mail → Ohne Beleg");
        vorgang.Dateiname.ShouldBe("");
        vorgang.MailMessageId.ShouldBe("news-7@shop.test");
        vorgang.MailBetreff.ShouldBe("Unser Newsletter");
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(0);
        (await greenMail.AnzahlAsync(OhneBeleg)).ShouldBe(1);
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(0);
    }

    [Fact]
    public async Task Signaturbild_unter_10_KB_zaehlt_nicht_als_Anhang()
    {
        await greenMail.SendenAsync(Nachricht("<logo@lieferant.test>", "Rechnung", ("rechnung.pdf", Pdf), ("logo.png", new byte[2 * 1024]), ("scan.jpg", new byte[20 * 1024])));
        await greenMail.SendenAsync(Nachricht("<nur-logo@lieferant.test>", "Gruß", ("logo.png", new byte[3 * 1024])));

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(2, 2, 1));

        _protokoll.Aufrufe.Select(a => a.Dateiname).Order().ShouldBe(["rechnung.pdf", "scan.jpg"]);
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(1);
        (await greenMail.AnzahlAsync(OhneBeleg)).ShouldBe(1);
    }

    [Fact]
    public async Task Als_inline_markiertes_PDF_zaehlt_als_Anhang()
    {
        var nachricht = Nachricht("<inline@lieferant.test>", "Rechnung");
        var teil = new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(Pdf)),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline) { FileName = "inline.pdf" },
            ContentTransferEncoding = ContentEncoding.Base64,
        };
        nachricht.Body = new Multipart("mixed") { new TextPart("plain") { Text = "Anbei" }, teil };
        await greenMail.SendenAsync(nachricht);

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(1, 1, 0));

        _protokoll.Aufrufe.ShouldHaveSingleItem().Dateiname.ShouldBe("inline.pdf");
    }

    [Fact]
    public async Task Fehlende_Zielordner_werden_angelegt()
    {
        using (var scope = Mail.ErzeugeScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(
                new MailEinstellungen { VerarbeitetOrdner = "Erledigt", OhneBelegOrdner = "Kein Beleg" },
                default);
        }

        (await greenMail.AnzahlAsync("Erledigt")).ShouldBeNull();
        (await greenMail.AnzahlAsync("Kein Beleg")).ShouldBeNull();

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(0, 0, 0));

        (await greenMail.AnzahlAsync("Erledigt")).ShouldBe(0);
        (await greenMail.AnzahlAsync("Kein Beleg")).ShouldBe(0);
    }

    [Fact]
    public async Task Zweiter_Abruf_derselben_Mail_nimmt_nichts_doppelt_an()
    {
        await greenMail.SendenAsync(Nachricht("<doppelt@lieferant.test>", "Rechnung", ("rechnung.pdf", Pdf)));
        await greenMail.SendenAsync(Nachricht("<news-doppelt@shop.test>", "Newsletter"));
        await Abruf.AbrufenAsync(default);

        await greenMail.ZurueckInDenEingangAsync(Verarbeitet);
        await greenMail.ZurueckInDenEingangAsync(OhneBeleg);
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(2);

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(2, 0, 1));

        _protokoll.Aufrufe.ShouldHaveSingleItem();
        (await VorgaengeAsync()).Count.ShouldBe(2);
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(0);
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(2);
        (await greenMail.AnzahlAsync(OhneBeleg)).ShouldBe(2);
    }

    [Fact]
    public async Task Ohne_Message_Id_gilt_ein_Ersatzschluessel_aus_Datum_Absender_und_Betreff()
    {
        var nachricht = Nachricht(null, "Rechnung ohne Kennung", ("rechnung.pdf", Pdf));
        nachricht.Headers.Remove(HeaderId.MessageId);
        await greenMail.SendenAsync(nachricht);
        await Abruf.AbrufenAsync(default);
        await greenMail.ZurueckInDenEingangAsync(Verarbeitet);

        await Abruf.AbrufenAsync(default);

        _protokoll.Aufrufe.ShouldHaveSingleItem().Herkunft.MailMessageId.ShouldStartWith("ersatz:");
    }

    [Fact]
    public async Task Fehler_bei_einer_Mail_laesst_sie_im_Eingang_und_der_Abruf_geht_weiter()
    {
        await greenMail.SendenAsync(Nachricht("<kaputt@lieferant.test>", "Kaputt", ("kaputt.pdf", Pdf)));
        await greenMail.SendenAsync(Nachricht("<heil@lieferant.test>", "Heil", ("heil.pdf", Pdf)));
        _protokoll.Fehlerhaft = "kaputt.pdf";

        (await Abruf.AbrufenAsync(default)).ShouldBe(new MailAbrufErgebnis(1, 1, 0));

        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(1);
        (await greenMail.AnzahlAsync(Verarbeitet)).ShouldBe(1);
    }

    [Fact]
    public async Task Job_haelt_das_Intervall_ein_und_laeuft_nur_wenn_aktiv()
    {
        var job = Mail.Services.GetServices<IWiederkehrenderJob>().OfType<MailAbrufJob>().ShouldHaveSingleItem();
        job.Id.ShouldBe("mail-abruf");
        job.Cron.ShouldBe("* * * * *");
        await greenMail.SendenAsync(Nachricht("<a@lieferant.test>", "A", ("a.pdf", Pdf)));

        await job.AusfuehrenAsync(default);
        _protokoll.Aufrufe.ShouldBeEmpty(); // Import ist standardmäßig aus

        await MailEinstellenAsync(new MailEinstellungen { Aktiv = true, AbrufintervallMinuten = 5 });
        await job.AusfuehrenAsync(default);
        _protokoll.Aufrufe.Count.ShouldBe(1);

        await greenMail.SendenAsync(Nachricht("<b@lieferant.test>", "B", ("b.pdf", Pdf)));
        Mail.Zeit.Vorspulen(TimeSpan.FromMinutes(4));
        await job.AusfuehrenAsync(default);
        _protokoll.Aufrufe.Count.ShouldBe(1);
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(1);

        Mail.Zeit.Vorspulen(TimeSpan.FromMinutes(1));
        await job.AusfuehrenAsync(default);
        _protokoll.Aufrufe.Count.ShouldBe(2);
        (await greenMail.AnzahlAsync("INBOX")).ShouldBe(0);
    }

    [Fact]
    public async Task Job_und_Status_ohne_IMAP_Zugang()
    {
        await using var ohne = fabrik.MitDiensten(d => d.AddSingleton(_protokoll).AddScoped<IImportDienst, FakeImport>());
        using (var scope = ohne.ErzeugeScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(new MailEinstellungen { Aktiv = true }, default);
        }

        await ohne.Services.GetServices<IWiederkehrenderJob>().OfType<MailAbrufJob>().Single().AusfuehrenAsync(default);

        _protokoll.Aufrufe.ShouldBeEmpty();
        var eintrag = await StatusQuelle(ohne).LesenAsync(default);
        eintrag.Schweregrad.ShouldBe(Schweregrad.Info);
        eintrag.Titel.ShouldBe("E-Mail-Import nicht eingerichtet");
        var antwort = await ohne.ErzeugeClient().PostAsync(ApiRouten.Einstellungen.MailJetztAbrufen, null);
        (await antwort.Content.LesenAsync<AktionErgebnisDto>())!.Erfolg.ShouldBeFalse();
    }

    [Fact]
    public async Task Jetzt_abrufen_meldet_Anzahlen_und_Status_zeigt_den_letzten_Abruf()
    {
        await greenMail.SendenAsync(Nachricht("<r1@lieferant.test>", "R1", ("r1.pdf", Pdf)));
        await greenMail.SendenAsync(Nachricht("<r2@lieferant.test>", "R2", ("r2.pdf", Pdf), ("r2.xml", "<Invoice/>"u8.ToArray())));
        await MailEinstellenAsync(new MailEinstellungen { Aktiv = true });
        var admin = Mail.ErzeugeClient(Rolle.Admin);

        var antwort = await admin.PostAsync(ApiRouten.Einstellungen.MailJetztAbrufen, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<AktionErgebnisDto>()).ShouldBe(new AktionErgebnisDto(true, "2 Mails, 3 Anhänge übernommen"));
        var status = await Mail.ErzeugeClient(Rolle.NurLesen).GetJsonAsync<MailStatusDto>(ApiRouten.Einstellungen.MailStatus);
        status.ShouldBe(new MailStatusDto(true, Mail.Zeit.GetUtcNow(), null));

        Mail.Zeit.Vorspulen(TimeSpan.FromMinutes(3));
        var eintrag = await StatusQuelle(Mail).LesenAsync(default);
        eintrag.Schweregrad.ShouldBe(Schweregrad.Ok);
        eintrag.Titel.ShouldBe("Postfach abgerufen");
        eintrag.Text.ShouldBe("vor 3 Min. · 3 neue Anhänge");
    }

    [Fact]
    public async Task Verbindungstest_gelingt_mit_richtigem_Passwort()
    {
        var antwort = await Mail.ErzeugeClient(Rolle.Admin).PostAsync(ApiRouten.Einstellungen.MailVerbindungTesten, null);

        var ergebnis = (await antwort.Content.LesenAsync<AktionErgebnisDto>())!;
        ergebnis.Erfolg.ShouldBeTrue(ergebnis.Meldung);
        ergebnis.Meldung.ShouldBe("Verbindung erfolgreich · 0 Mails im Eingang");
    }

    [Fact]
    public async Task Falsches_Passwort_scheitert_ohne_das_Passwort_zu_verraten()
    {
        const string falsch = "falsch-XYZ-4711";
        await using var falscheFabrik = MitPostfach(falsch);
        using (var scope = falscheFabrik.ErzeugeScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(new MailEinstellungen { Aktiv = true }, default);
        }

        var admin = falscheFabrik.ErzeugeClient(Rolle.Admin);

        var test = (await (await admin.PostAsync(ApiRouten.Einstellungen.MailVerbindungTesten, null)).Content.LesenAsync<AktionErgebnisDto>())!;
        test.Erfolg.ShouldBeFalse();
        test.Meldung.ShouldNotContain(falsch);
        test.Meldung.ShouldContain("Anmeldung");

        var abruf = (await (await admin.PostAsync(ApiRouten.Einstellungen.MailJetztAbrufen, null)).Content.LesenAsync<AktionErgebnisDto>())!;
        abruf.Erfolg.ShouldBeFalse();
        abruf.Meldung.ShouldNotContain(falsch);

        var status = (await admin.GetJsonAsync<MailStatusDto>(ApiRouten.Einstellungen.MailStatus))!;
        status.Verbunden.ShouldBeFalse();
        status.Fehler.ShouldBe(abruf.Meldung);
        var eintrag = await StatusQuelle(falscheFabrik).LesenAsync(default);
        eintrag.Schweregrad.ShouldBe(Schweregrad.Fehler);
        eintrag.Text.ShouldBe(abruf.Meldung);
    }

    [Theory]
    [InlineData(ApiRouten.Einstellungen.MailVerbindungTesten)]
    [InlineData(ApiRouten.Einstellungen.MailJetztAbrufen)]
    public async Task Aktionen_nur_fuer_Admin(string route)
    {
        (await Mail.ErzeugeClient(Rolle.Benutzer).PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Mail.ErzeugeClient(Rolle.NurLesen).PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Mail.ErzeugeAnonymenClient().PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        _protokoll.Aufrufe.ShouldBeEmpty();
    }

    [Fact]
    public async Task Status_lesen_ab_NurLesen()
    {
        (await Mail.ErzeugeClient(Rolle.NurLesen).GetAsync(ApiRouten.Einstellungen.MailStatus)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Mail.ErzeugeAnonymenClient().GetAsync(ApiRouten.Einstellungen.MailStatus)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static ISystemStatusQuelle StatusQuelle(ApiFabrik f) =>
        f.Services.GetServices<ISystemStatusQuelle>().Single(q => q.Schluessel == "postfach");

    private ApiFabrik MitPostfach(string passwort) => fabrik.MitDiensten(d =>
    {
        d.PostConfigure<ImapZugang>(z =>
        {
            z.Host = greenMail.Host;
            z.Port = greenMail.Imap;
            z.Benutzer = GreenMail.Benutzer;
            z.Passwort = passwort;
            z.Ssl = false;
        });
        d.AddSingleton(_protokoll);
        d.AddScoped<IImportDienst, FakeImport>();
    });

    private async Task MailEinstellenAsync(MailEinstellungen einstellungen)
    {
        using var scope = Mail.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(einstellungen, default);
    }

    private async Task<List<ImportVorgang>> VorgaengeAsync()
    {
        using var scope = Mail.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().ImportVorgaenge.AsNoTracking().ToListAsync();
    }

    private static MimeMessage Nachricht(string? messageId, string betreff, params (string Name, byte[] Inhalt)[] anhaenge)
    {
        var nachricht = new MimeMessage { Subject = betreff };
        nachricht.From.Add(MailboxAddress.Parse(messageId is null ? "ohne-id@lieferant.test" : "rechnung@stadtwerke.test"));
        if (messageId is not null)
        {
            nachricht.MessageId = messageId.Trim('<', '>');
        }

        var body = new BodyBuilder { TextBody = "Guten Tag, anbei die Unterlagen." };
        foreach (var (name, inhalt) in anhaenge)
        {
            body.Attachments.Add(name, inhalt);
        }

        nachricht.Body = body.ToMessageBody();
        return nachricht;
    }

    public sealed record Aufruf(string Dateiname, ImportQuelle Quelle, ImportHerkunft Herkunft, long Groesse);

    /// <summary>Was der <see cref="FakeImport"/> angenommen hat; <see cref="Fehlerhaft"/> lässt die Annahme dieser Datei scheitern.</summary>
    public sealed class ImportProtokoll
    {
        private readonly List<Aufruf> _aufrufe = [];

        public string? Fehlerhaft { get; set; }

        public IReadOnlyList<Aufruf> Aufrufe
        {
            get
            {
                lock (_aufrufe)
                {
                    return [.. _aufrufe];
                }
            }
        }

        public void Hinzufuegen(Aufruf aufruf)
        {
            lock (_aufrufe)
            {
                _aufrufe.Add(aufruf);
            }
        }
    }

    /// <summary>Statt der Pipeline: protokolliert den Aufruf und legt nur den <see cref="ImportVorgang"/> an.</summary>
    private sealed class FakeImport(ImportProtokoll protokoll, BiberDbContext db, TimeProvider zeit) : IImportDienst
    {
        public async Task<ImportVorgangDto> AnnehmenAsync(Stream inhalt, string dateiname, ImportQuelle quelle, ImportHerkunft herkunft, CancellationToken ct)
        {
            if (dateiname == protokoll.Fehlerhaft)
            {
                throw new InvalidOperationException("Staging nicht erreichbar");
            }

            using var puffer = new MemoryStream();
            await inhalt.CopyToAsync(puffer, ct);
            protokoll.Hinzufuegen(new Aufruf(dateiname, quelle, herkunft, puffer.Length));
            db.ImportVorgaenge.Add(new ImportVorgang
            {
                Id = Guid.NewGuid(),
                Quelle = quelle,
                Status = ImportStatus.Angenommen,
                Dateiname = Path.GetFileName(dateiname),
                MailMessageId = herkunft.MailMessageId,
                MailAbsender = herkunft.MailAbsender,
                MailBetreff = herkunft.MailBetreff,
                ErstelltAm = zeit.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
            return null!;
        }

        public Task<SeiteDto<ImportVorgangDto>> VorgaengeAsync(ImportQuelle? quelle, int tage, int seite, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ImportVorgangDto?> VorgangAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> PausierteFortsetzenAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
