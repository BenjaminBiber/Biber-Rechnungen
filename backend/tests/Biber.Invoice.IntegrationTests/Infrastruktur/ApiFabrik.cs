using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>Alle Integrationstests mit <c>[Collection("Api")]</c> teilen sich eine <see cref="ApiFabrik"/>.</summary>
[CollectionDefinition("Api")]
public sealed class ApiSammlung : ICollectionFixture<ApiFabrik>;

/// <summary>
/// Startet die API gegen eine Postgres-Instanz in Testcontainers (<c>postgres:17</c>).
/// Anmeldung über das Schema „Test“ (<see cref="TestAuthHandler"/>), Zeit über <see cref="Zeit"/>,
/// Dateipfade (<c>Pfade:*</c>) unter <see cref="TempOrdner"/>.
/// </summary>
public sealed class ApiFabrik : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly bool _besitztContainer;
    private readonly IReadOnlyList<Action<IServiceCollection>> _aenderungen;
    private readonly bool _echteAnmeldung;
    private readonly string? _datenOrdner;
    private readonly List<ApiFabrik> _abgeleitete = [];
    private Respawner? _respawner;
    private string? _seedSql;
    private bool _entsorgt;

    /// <summary>
    /// Ohne Neuladen bei Dateiänderungen: Sonst überwacht jeder Test-Host die appsettings-Dateien, und unter Linux
    /// ist das inotify-Limit (meist 128 Instanzen je Benutzer) nach einigen Dutzend Hosts erschöpft.
    /// </summary>
    static ApiFabrik() => Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

    public ApiFabrik()
        : this(new PostgreSqlBuilder("postgres:17").Build(), besitztContainer: true, [], echteAnmeldung: false, datenOrdner: null)
    {
    }

    private ApiFabrik(
        PostgreSqlContainer postgres,
        bool besitztContainer,
        IReadOnlyList<Action<IServiceCollection>> aenderungen,
        bool echteAnmeldung,
        string? datenOrdner)
    {
        _postgres = postgres;
        _besitztContainer = besitztContainer;
        _aenderungen = aenderungen;
        _echteAnmeldung = echteAnmeldung;
        _datenOrdner = datenOrdner;
        TempOrdner = Path.Combine(Path.GetTempPath(), "biber-invoice-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(TempOrdner, "wwwroot"));
    }

    /// <summary>Ordner, den die Anwendung als <c>Pfade:Daten</c> bekommt (darin liegt der Schlüsselbund).</summary>
    public string DatenOrdner => _datenOrdner ?? Path.Combine(TempOrdner, "daten");

    /// <summary>Eigener Ordner dieser Fabrik; <c>Pfade:*</c> und <c>wwwroot</c> liegen darunter.</summary>
    public string TempOrdner { get; }

    /// <summary>Die Uhr der Anwendung (ersetzt <see cref="TimeProvider.System"/>).</summary>
    public FesteZeit Zeit { get; } = new();

    /// <summary>Feste Benutzer-ID, die <see cref="ErzeugeClient"/> für eine Rolle sendet.</summary>
    public static Guid BenutzerId(Rolle rolle) => new($"00000000-0000-0000-0000-{(int)rolle + 1:D12}");

    public HttpClient ErzeugeClient(Rolle rolle = Rolle.Admin)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolleHeader, rolle.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.BenutzerHeader, BenutzerId(rolle).ToString());
        return client;
    }

    public HttpClient ErzeugeAnonymenClient() => CreateClient();

    /// <summary>
    /// Neue Fabrik auf derselben Datenbank, deren Dienste zusätzlich <paramref name="aendern"/> durchlaufen
    /// (z. B. Fakes statt echter Dienste). Wird mit dieser Fabrik entsorgt.
    /// </summary>
    public ApiFabrik MitDiensten(Action<IServiceCollection> aendern) =>
        Ableiten([.. _aenderungen, aendern], _echteAnmeldung, _datenOrdner);

    /// <summary>Neue Fabrik ohne Testschema: Es gilt die echte Anmeldung der Anwendung.</summary>
    public ApiFabrik MitEchterAnmeldung() => Ableiten(_aenderungen, echteAnmeldung: true, _datenOrdner);

    /// <summary>
    /// Neue Fabrik, deren Anwendung <paramref name="pfad"/> als <c>Pfade:Daten</c> nutzt – etwa der Ordner einer
    /// anderen Fabrik, um einen Neustart der Anwendung nachzustellen.
    /// </summary>
    public ApiFabrik MitDatenOrdner(string pfad) => Ableiten(_aenderungen, _echteAnmeldung, pfad);

    public IServiceScope ErzeugeScope() => Services.CreateScope();

    /// <summary>
    /// Leert alle Tabellen außer <c>__EFMigrationsHistory</c> und stellt den Seed der Migration wieder her
    /// (Rollen, System-Tags; Quelle ist <c>HasData</c> im EF-Modell).
    /// </summary>
    public async Task ZuruecksetzenAsync()
    {
        _ = Server; // startet die Anwendung und damit die Migration
        await using var verbindung = new NpgsqlConnection(_postgres.GetConnectionString());
        await verbindung.OpenAsync();
        if (_respawner is null && !await HatTabellenAsync(verbindung))
        {
            return; // Respawn verweigert eine Datenbank ohne Tabellen; dann gibt es nichts zu leeren.
        }

        _respawner ??= await Respawner.CreateAsync(verbindung, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
            // Hangfire verwaltet sein Schema selbst: hangfire.schema hält die Schema-Version (geleert installiert
            // Hangfire bei jedem Start neu und scheitert), und geleerte Tabellen lassen das Eintragen
            // wiederkehrender Jobs beim nächsten Start der Anwendung hängen.
            SchemasToExclude = ["hangfire"],
        });
        await _respawner.ResetAsync(verbindung);

        _seedSql ??= SeedSqlErzeugen();
        await using (var befehl = new NpgsqlCommand(_seedSql, verbindung))
        {
            await befehl.ExecuteNonQueryAsync();
        }

        // Zwischengespeicherte Einstellungen gehören zur geleerten Datenbank und dürfen den nächsten Test nicht beeinflussen.
        (Services.GetService<IMemoryCache>() as MemoryCache)?.Clear();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseWebRoot(Path.Combine(TempOrdner, "wwwroot"));
        builder.UseSetting("ConnectionStrings:Datenbank", _postgres.GetConnectionString());
        foreach (var pfad in new[] { "Eingang", "Staging", "Ablage", "Backup" })
        {
            builder.UseSetting($"Pfade:{pfad}", Path.Combine(TempOrdner, pfad.ToLowerInvariant()));
        }

        builder.UseSetting("Pfade:Daten", DatenOrdner);

        builder.UseSetting("Verfahrensdoku:Pfad", Path.Combine(TempOrdner, "verfahrensdokumentation"));
        builder.UseSetting("OrdnerWatcher:Aktiv", "false");
        builder.UseSetting("Hangfire:ServerAktiv", "false"); // Jobs laufen in Tests über SofortPlaner

        builder.ConfigureTestServices(dienste =>
        {
            dienste.RemoveAll<TimeProvider>();
            dienste.AddSingleton<TimeProvider>(Zeit);

            if (!_echteAnmeldung)
            {
                dienste.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Schema, _ => { });
                dienste.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = TestAuthHandler.Schema;
                    o.DefaultAuthenticateScheme = TestAuthHandler.Schema;
                    o.DefaultChallengeScheme = TestAuthHandler.Schema;
                    o.DefaultForbidScheme = TestAuthHandler.Schema;
                });
            }

            foreach (var aendern in _aenderungen)
            {
                aendern(dienste);
            }
        });
    }

    public async Task InitializeAsync()
    {
        if (_besitztContainer)
        {
            await _postgres.StartAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync() => await DisposeAsync();

    public override async ValueTask DisposeAsync()
    {
        if (_entsorgt)
        {
            return;
        }

        _entsorgt = true;
        foreach (var abgeleitet in _abgeleitete)
        {
            await abgeleitet.DisposeAsync();
        }

        await base.DisposeAsync();
        if (_besitztContainer)
        {
            await _postgres.DisposeAsync();
        }

        try
        {
            // Die Ablage setzt ihre Dateien schreibgeschützt; das würde das rekursive Löschen verhindern.
            foreach (var datei in Directory.EnumerateFiles(TempOrdner, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(datei, FileAttributes.Normal);
            }

            Directory.Delete(TempOrdner, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Aufräumen ist Kür; gesperrte Dateien bleiben im Temp-Ordner liegen.
        }
    }

    private static async Task<bool> HatTabellenAsync(NpgsqlConnection verbindung)
    {
        await using var befehl = new NpgsqlCommand(
            """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema NOT IN ('pg_catalog', 'information_schema')
                  AND table_name <> '__EFMigrationsHistory')
            """,
            verbindung);
        return (bool)(await befehl.ExecuteScalarAsync())!;
    }

    /// <summary>Die <c>INSERT</c>s, die eine Migration von leer auf das aktuelle Modell für <c>HasData</c> ausführen würde.</summary>
    private string SeedSqlErzeugen()
    {
        using var scope = ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var modell = db.GetService<IDesignTimeModel>().Model;
        var einfuegen = db.GetService<IMigrationsModelDiffer>()
            .GetDifferences(null, modell.GetRelationalModel())
            .OfType<InsertDataOperation>()
            .ToList<MigrationOperation>();
        var befehle = db.GetService<IMigrationsSqlGenerator>().Generate(einfuegen, modell);
        return string.Concat(befehle.Select(b => b.CommandText));
    }

    private ApiFabrik Ableiten(IReadOnlyList<Action<IServiceCollection>> aenderungen, bool echteAnmeldung, string? datenOrdner)
    {
        var fabrik = new ApiFabrik(_postgres, besitztContainer: false, aenderungen, echteAnmeldung, datenOrdner);
        _abgeleitete.Add(fabrik);
        return fabrik;
    }
}
