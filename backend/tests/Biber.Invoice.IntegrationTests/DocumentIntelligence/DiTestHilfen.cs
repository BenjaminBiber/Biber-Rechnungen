using System.Text;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.DocumentIntelligence;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

/// <summary>Document Intelligence als Fake: zählt Aufrufe und liefert eine feste Extraktion oder einen Fehler.</summary>
public sealed class FakeDiClient : IDocumentIntelligenceClient
{
    private int _aufrufe;

    public bool IstKonfiguriert { get; set; } = true;

    public int Seiten { get; set; } = 1;

    public Exception? Fehler { get; set; }

    public int Aufrufe => _aufrufe;

    public Task<ExtraktionsErgebnis> AnalysierenAsync(Stream datei, CancellationToken ct)
    {
        Interlocked.Increment(ref _aufrufe);
        if (Fehler is not null)
        {
            throw Fehler;
        }

        return Task.FromResult(new ExtraktionsErgebnis
        {
            Quelle = ExtraktionsQuelle.DocumentIntelligence,
            Belegart = Belegart.Rechnung,
            Rechnungsnummer = "RE-2026-0815",
            Brutto = 119m,
            Konfidenz = 0.9m,
            Seiten = Seiten,
        });
    }

    public Task<bool> VerbindungTestenAsync(CancellationToken ct) => Task.FromResult(IstKonfiguriert);
}

/// <summary>Ersetzt die echte Formaterkennung (sie würde die Platzhalter-Dateien ablehnen): setzt Format und Seitenzahl des Belegs.</summary>
public sealed class FormatSchritt(ErkanntesFormat format, int seiten) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.Formaterkennung;

    public string Name => "Formaterkennung";

    public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        kontext.Format = format;
        kontext.Beleg.Seitenanzahl = seiten;
        return Task.FromResult(SchrittErgebnis.Weiter);
    }
}

public static class DiTestHilfen
{
    /// <summary>Jeder Zugriff liefert andere Bytes – gleiche Dateien würde der Dubletten-Check (SHA-256) als Dublette beenden.</summary>
    public static byte[] Pdf => Encoding.UTF8.GetBytes($"%PDF-1.7 {Guid.NewGuid():N}");

    /// <summary>Fabrik mit sofortiger Verarbeitung, <paramref name="client"/> als Document Intelligence und erkanntem Format.</summary>
    public static ApiFabrik MitDi(this ApiFabrik fabrik, FakeDiClient client, ErkanntesFormat format = ErkanntesFormat.Pdf, int seiten = 1) =>
        fabrik.MitSofortVerarbeitung().MitDiensten(d =>
        {
            d.RemoveAll<IDocumentIntelligenceClient>();
            d.AddSingleton<IDocumentIntelligenceClient>(client);
            foreach (var eintrag in d.Where(e => e.ServiceType == typeof(IPipelineSchritt) && e.ImplementationType == typeof(FormaterkennungSchritt)).ToList())
            {
                d.Remove(eintrag);
            }

            d.AddSingleton<IPipelineSchritt>(new FormatSchritt(format, seiten));
        });

    public static async Task DiEinstellenAsync(this ApiFabrik f, int limit = 500, int warnungAb = 80, int maxSeiten = 10)
    {
        using var scope = f.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(
            new DiEinstellungen { Monatslimit = limit, WarnungAbProzent = warnungAb, MaxSeitenJeBeleg = maxSeiten },
            CancellationToken.None);
    }

    /// <summary>Setzt den Verbrauch des Monats <paramref name="monat"/> (Monatserster).</summary>
    public static async Task VerbrauchSetzenAsync(this ApiFabrik f, DateOnly monat, int seiten)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        db.DiVerbraeuche.Add(new DiVerbrauch { Monat = monat, Seiten = seiten });
        await db.SaveChangesAsync();
    }

    public static async Task<int> VerbrauchAsync(this ApiFabrik f, DateOnly monat)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.DiVerbraeuche.Where(d => d.Monat == monat).Select(d => d.Seiten).FirstOrDefaultAsync();
    }

    public static async Task<List<Benachrichtigung>> MeldungenAsync(this ApiFabrik f, BenachrichtigungArt art)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Benachrichtigungen.AsNoTracking().Where(b => b.Art == art).ToListAsync();
    }

    public static async Task<Beleg> BelegAsync(this ApiFabrik f, Guid id)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking().SingleAsync(b => b.Id == id);
    }

    public static async Task<DiVerbrauchDto> VerbrauchLesenAsync(this ApiFabrik f)
    {
        using var scope = f.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<DiVerbrauchsZaehler>().LesenAsync(CancellationToken.None);
    }
}
