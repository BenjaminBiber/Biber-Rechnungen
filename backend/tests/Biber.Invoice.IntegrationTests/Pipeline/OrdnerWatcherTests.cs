using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.OrdnerWatcher;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Biber.Invoice.IntegrationTests.Pipeline;

public sealed class OrdnerWatcherTests : IDisposable
{
    private readonly string _tempOrdner = Path.Combine(Path.GetTempPath(), "biber-invoice-tests", Guid.NewGuid().ToString("N"));
    private readonly FesteZeit _zeit = new();
    private readonly FakeImport _import = new();
    private readonly ServiceProvider _dienste;

    public OrdnerWatcherTests()
    {
        Directory.CreateDirectory(Eingang);
        var konfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Pfade:Eingang"] = Eingang, ["OrdnerWatcher:Aktiv"] = "false" })
            .Build();
        var dienste = new ServiceCollection();
        dienste.AddSingleton<IConfiguration>(konfiguration);
        dienste.AddSingleton<TimeProvider>(_zeit);
        dienste.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        dienste.AddSingleton<IImportDienst>(_import);
        dienste.AddSingleton<IEinstellungen>(new FesteEinstellungen());
        new OrdnerWatcherModul().Registrieren(dienste, konfiguration);
        _dienste = dienste.BuildServiceProvider();
    }

    private string Eingang => Path.Combine(_tempOrdner, "eingang");

    private OrdnerAbtaster Abtaster => _dienste.GetRequiredService<OrdnerAbtaster>();

    [Fact]
    public async Task Datei_wird_erst_nach_10_s_stabiler_Groesse_uebernommen_und_danach_geloescht()
    {
        Schreibe("rechnung.pdf", 100);

        (await Abtaster.AbtastenAsync(default)).ShouldBe(0);
        _zeit.Vorspulen(TimeSpan.FromSeconds(9));
        (await Abtaster.AbtastenAsync(default)).ShouldBe(0);
        _zeit.Vorspulen(TimeSpan.FromSeconds(1));
        (await Abtaster.AbtastenAsync(default)).ShouldBe(1);

        _import.Aufrufe.ShouldHaveSingleItem().ShouldBe(("rechnung.pdf", ImportQuelle.Ordner, "rechnung.pdf", 100L));
        Directory.EnumerateFileSystemEntries(Eingang).ShouldBeEmpty();
    }

    [Fact]
    public async Task Waechst_die_Datei_beginnt_die_Frist_neu()
    {
        Schreibe("rechnung.pdf", 100);
        await Abtaster.AbtastenAsync(default);
        _zeit.Vorspulen(TimeSpan.FromSeconds(8));
        Schreibe("rechnung.pdf", 200);
        await Abtaster.AbtastenAsync(default);

        _zeit.Vorspulen(TimeSpan.FromSeconds(8));
        (await Abtaster.AbtastenAsync(default)).ShouldBe(0);
        _zeit.Vorspulen(TimeSpan.FromSeconds(2));
        (await Abtaster.AbtastenAsync(default)).ShouldBe(1);
        _import.Aufrufe.ShouldHaveSingleItem().Groesse.ShouldBe(200);
    }

    [Fact]
    public async Task Exklusiv_gesperrte_Datei_wartet()
    {
        Schreibe("rechnung.pdf", 100);
        await Abtaster.AbtastenAsync(default);
        _zeit.Vorspulen(TimeSpan.FromSeconds(10));

        using (new FileStream(Path.Combine(Eingang, "rechnung.pdf"), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            (await Abtaster.AbtastenAsync(default)).ShouldBe(0);
        }

        _import.Aufrufe.ShouldBeEmpty();
        (await Abtaster.AbtastenAsync(default)).ShouldBe(1);
    }

    [Fact]
    public async Task Heic_landet_in_abgelehnt_und_wird_genau_einmal_gemeldet()
    {
        Schreibe("foto.heic", 50);
        await StabilAbtastenAsync();
        await StabilAbtastenAsync();

        _import.Aufrufe.ShouldHaveSingleItem().Dateiname.ShouldBe("foto.heic");
        File.Exists(Path.Combine(Eingang, OrdnerAbtaster.AbgelehntOrdner, "foto.heic")).ShouldBeTrue();
        Directory.EnumerateFiles(Eingang).ShouldBeEmpty();

        Schreibe("foto.heic", 60);
        await StabilAbtastenAsync();
        Directory.EnumerateFiles(Path.Combine(Eingang, OrdnerAbtaster.AbgelehntOrdner)).Count().ShouldBe(2);
        _import.Aufrufe.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Versteckte_und_temporaere_Dateien_werden_ignoriert()
    {
        Schreibe(".DS_Store", 10);
        Schreibe("~$rechnung.pdf", 10);
        Schreibe("scan.tmp", 10);
        Schreibe("scan.pdf.part", 10);
        // Das Attribut „Versteckt“ gibt es nur unter Windows; unter Linux/macOS ist eine Datei durch den Punkt versteckt.
        var versteckt = OperatingSystem.IsWindows() ? "versteckt.pdf" : ".versteckt.pdf";
        Schreibe(versteckt, 10);
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(Path.Combine(Eingang, versteckt), FileAttributes.Hidden);
        }
        Directory.CreateDirectory(Path.Combine(Eingang, "unterordner"));
        File.WriteAllBytes(Path.Combine(Eingang, "unterordner", "tief.pdf"), new byte[10]);

        (await StabilAbtastenAsync()).ShouldBe(0);

        _import.Aufrufe.ShouldBeEmpty();
        Directory.EnumerateFiles(Eingang).Count().ShouldBe(5);
    }

    [Fact]
    public async Task Zwei_Dateien_in_einer_Abtastung()
    {
        Schreibe("a.pdf", 10);
        Schreibe("b.xml", 20);

        (await StabilAbtastenAsync()).ShouldBe(2);

        _import.Aufrufe.Select(a => a.Dateiname).Order().ShouldBe(["a.pdf", "b.xml"]);
        Directory.EnumerateFileSystemEntries(Eingang).ShouldBeEmpty();
    }

    [Fact]
    public async Task Status_Quelle_zaehlt_wartende_Dateien()
    {
        Schreibe("a.pdf", 10);
        Schreibe("b.pdf", 10);
        Schreibe("scan.tmp", 10);
        var quelle = _dienste.GetServices<ISystemStatusQuelle>().ShouldHaveSingleItem();

        var eintrag = await quelle.LesenAsync(default);

        quelle.Schluessel.ShouldBe("eingangsordner");
        eintrag.Schweregrad.ShouldBe(Schweregrad.Ok);
        eintrag.Titel.ShouldBe("Eingangsordner überwacht");
        eintrag.Text.ShouldBe("Rechnungen-Eingang · 2 Dateien wartend");

        Directory.Delete(Eingang, recursive: true);
        (await quelle.LesenAsync(default)).Schweregrad.ShouldBe(Schweregrad.Fehler);
    }

    [Fact]
    public async Task Fehler_beim_Annehmen_laesst_die_Datei_liegen_und_wird_erneut_versucht()
    {
        Schreibe("rechnung.pdf", 100);
        _import.Fehler = true;

        (await StabilAbtastenAsync()).ShouldBe(0);
        File.Exists(Path.Combine(Eingang, "rechnung.pdf")).ShouldBeTrue();

        _import.Fehler = false;
        (await Abtaster.AbtastenAsync(default)).ShouldBe(1);
        _import.Aufrufe.Count.ShouldBe(2);
        Directory.EnumerateFileSystemEntries(Eingang).ShouldBeEmpty();
    }

    public void Dispose()
    {
        _dienste.Dispose();
        if (Directory.Exists(_tempOrdner))
        {
            Directory.Delete(_tempOrdner, recursive: true);
        }
    }

    private async Task<int> StabilAbtastenAsync()
    {
        var anzahl = await Abtaster.AbtastenAsync(default);
        _zeit.Vorspulen(TimeSpan.FromSeconds(10));
        return anzahl + await Abtaster.AbtastenAsync(default);
    }

    private void Schreibe(string name, int groesse) => File.WriteAllBytes(Path.Combine(Eingang, name), new byte[groesse]);

    private sealed class FakeImport : IImportDienst
    {
        public List<(string Dateiname, ImportQuelle Quelle, string? Pfad, long Groesse)> Aufrufe { get; } = [];

        public bool Fehler { get; set; }

        public async Task<ImportVorgangDto> AnnehmenAsync(Stream inhalt, string dateiname, ImportQuelle quelle, ImportHerkunft herkunft, CancellationToken ct)
        {
            using var puffer = new MemoryStream();
            await inhalt.CopyToAsync(puffer, ct);
            Aufrufe.Add((dateiname, quelle, herkunft.Pfad, puffer.Length));
            return Fehler ? throw new InvalidOperationException("Datenbank nicht erreichbar") : null!;
        }

        public Task<SeiteDto<ImportVorgangDto>> VorgaengeAsync(ImportQuelle? quelle, int tage, int seite, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ImportVorgangDto?> VorgangAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();

        public Task<int> PausierteFortsetzenAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task<ImportVorgangDto?> AlsBelegUebernehmenAsync(Guid id, Guid? benutzerId, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FesteEinstellungen : IEinstellungen
    {
        public Task<T> LesenAsync<T>(CancellationToken ct) where T : class, new() =>
            Task.FromResult(typeof(T) == typeof(OrdnerEinstellungen) ? (T)(object)new OrdnerEinstellungen() : new T());

        public Task SpeichernAsync<T>(T wert, CancellationToken ct) where T : class => throw new NotSupportedException();
    }
}
