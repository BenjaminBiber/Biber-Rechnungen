using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Biber.Invoice.IntegrationTests.Persistenz;

/// <summary>
/// Eigene, leere Datenbank im Postgres-Container der <see cref="ApiFabrik"/>. Unabhängig von anderen Tests,
/// die die gemeinsame Datenbank zurücksetzen. Wird beim Entsorgen gelöscht.
/// </summary>
internal sealed class FrischeDatenbank : IAsyncDisposable
{
    private readonly string _verwaltung;
    private readonly string _name = $"frisch_{Guid.NewGuid():N}";

    private FrischeDatenbank(string verbindung)
    {
        _verwaltung = verbindung;
        Verbindung = new NpgsqlConnectionStringBuilder(verbindung) { Database = _name }.ConnectionString;
    }

    public string Verbindung { get; }

    public static async Task<FrischeDatenbank> AnlegenAsync(ApiFabrik fabrik)
    {
        var verbindung = fabrik.Services.GetRequiredService<IConfiguration>().GetConnectionString("Datenbank")!;
        var datenbank = new FrischeDatenbank(verbindung);
        await using var conn = new NpgsqlConnection(verbindung);
        await conn.OpenAsync();
        await using var befehl = new NpgsqlCommand($"CREATE DATABASE {datenbank._name}", conn);
        await befehl.ExecuteNonQueryAsync();
        return datenbank;
    }

    public BiberDbContext Kontext() =>
        new(new DbContextOptionsBuilder<BiberDbContext>().UseNpgsql(Verbindung).Options);

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_verwaltung);
        await conn.OpenAsync();
        await using var befehl = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", conn);
        await befehl.ExecuteNonQueryAsync();
    }
}
