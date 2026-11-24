using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Kosit;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Kosit;

[Collection("Api")]
public sealed class KositSchrittTests(ApiFabrik fabrik)
{
    private static readonly byte[] Xml = Encoding.UTF8.GetBytes("<Invoice/>");

    [Fact]
    public async Task Visualisierung_liegt_als_Datei_und_BelegDatei_vor_und_zweiter_Lauf_ersetzt()
    {
        var kosit = new FakeKosit { Html = "<html>RE-1</html>" };
        var f = Fabrik(kosit);
        var beleg = await f.SpeichernAsync(Testdaten.Beleg());

        var kontext = await AusfuehrenAsync(f, beleg, Xml, SchrittErgebnis.Weiter);

        kontext.Kosit.ShouldBe(kosit.Ergebnis);
        kontext.VisualisierungHtml.ShouldBe("<html>RE-1</html>");
        var pfad = Path.Combine(f.TempOrdner, "daten", "visualisierung", $"{beleg.Id}.html");
        (await File.ReadAllTextAsync(pfad)).ShouldBe("<html>RE-1</html>");

        kosit.Html = "<html>RE-2</html>";
        await AusfuehrenAsync(f, beleg, Xml, SchrittErgebnis.Weiter);

        (await File.ReadAllTextAsync(pfad)).ShouldBe("<html>RE-2</html>");
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var datei = await db.BelegDateien.SingleAsync(d => d.BelegId == beleg.Id);
        datei.Rolle.ShouldBe(DateiRolle.Visualisierung);
        datei.MimeType.ShouldBe("text/html");
        datei.Groesse.ShouldBe(Encoding.UTF8.GetByteCount("<html>RE-2</html>"));
        datei.Sha256.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("<html>RE-2</html>"))));
        datei.Pfad.ShouldBe(Path.Combine("visualisierung", $"{beleg.Id}.html"));
    }

    [Fact]
    public async Task PDF_ohne_XML_wird_uebersprungen()
    {
        var kosit = new FakeKosit();
        var f = Fabrik(kosit);
        var beleg = await f.SpeichernAsync(Testdaten.Beleg());

        var kontext = await AusfuehrenAsync(f, beleg, null, SchrittErgebnis.Weiter);

        kosit.Aufrufe.ShouldBe(0);
        kontext.Kosit.ShouldBeNull();
        kontext.VisualisierungHtml.ShouldBeNull();
    }

    [Fact]
    public async Task Nicht_erreichbar_liefert_SpaeterErneut()
    {
        var f = Fabrik(new FakeKosit { NichtErreichbar = true });
        var beleg = await f.SpeichernAsync(Testdaten.Beleg());

        await AusfuehrenAsync(f, beleg, Xml, SchrittErgebnis.SpaeterErneut("KoSIT-Validator nicht erreichbar"));
    }

    private ApiFabrik Fabrik(FakeKosit kosit) => fabrik.MitDiensten(d =>
    {
        d.RemoveAll<IKositClient>();
        d.AddSingleton<IKositClient>(kosit);
    });

    private static async Task<VerarbeitungsKontext> AusfuehrenAsync(ApiFabrik f, Beleg beleg, byte[]? xml, SchrittErgebnis erwartet)
    {
        using var scope = f.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<KositSchritt>().Single();
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "rechnung.xml", Status = ImportStatus.InVerarbeitung },
            Beleg = beleg,
            DateiPfad = Path.Combine(f.TempOrdner, "staging", "rechnung.xml"),
            Dateiname = "rechnung.xml",
            ERechnungXml = xml,
        };

        (await schritt.AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(erwartet);
        await scope.ServiceProvider.GetRequiredService<BiberDbContext>().SaveChangesAsync();
        return kontext;
    }

    private sealed class FakeKosit : IKositClient
    {
        public KositErgebnis Ergebnis { get; } = new(false, ["BR-DE-15 Käuferreferenz fehlt"]);
        public string Html { get; set; } = "<html/>";
        public bool NichtErreichbar { get; init; }
        public int Aufrufe { get; private set; }

        public Task<KositErgebnis> ValidierenAsync(byte[] xml, CancellationToken ct)
        {
            Aufrufe++;
            return NichtErreichbar ? throw new KositNichtErreichbarException("weg") : Task.FromResult(Ergebnis);
        }

        public Task<string> VisualisierenAsync(byte[] xml, CancellationToken ct)
        {
            Aufrufe++;
            return NichtErreichbar ? throw new KositNichtErreichbarException("weg") : Task.FromResult(Html);
        }
    }
}
