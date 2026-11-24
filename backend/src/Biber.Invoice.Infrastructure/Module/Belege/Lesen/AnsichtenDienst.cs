using System.Text.Json;
using System.Text.Json.Serialization;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Belege.Lesen;

/// <summary>
/// Gespeicherte Ansichten der Belegliste je Benutzer. <see cref="GespeicherteAnsicht.Definition"/> hält Filter,
/// Spalten und Darstellung als JSON; ein gleichnamiges Speichern überschreibt die vorhandene Ansicht.
/// </summary>
public sealed class AnsichtenDienst(BiberDbContext db)
{
    public const int MaxNameLaenge = 200;
    public static readonly string[] Darstellungen = ["Tabelle", "KleineKarten", "GrosseKarten"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<GespeicherteAnsichtDto>> ListeAsync(Guid benutzerId, CancellationToken ct)
    {
        var ansichten = await db.GespeicherteAnsichten
            .AsNoTracking()
            .Where(a => a.BenutzerId == benutzerId)
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);
        return [.. ansichten.Select(Dto)];
    }

    /// <summary>Legt die Ansicht an oder überschreibt die gleichnamige des Benutzers.</summary>
    public async Task<GespeicherteAnsichtDto> SpeichernAsync(Guid benutzerId, GespeicherteAnsichtAnlegenDto eingabe, CancellationToken ct)
    {
        var name = eingabe.Name.Trim();
        var ansicht = await db.GespeicherteAnsichten.FirstOrDefaultAsync(a => a.BenutzerId == benutzerId && a.Name == name, ct);
        if (ansicht is null)
        {
            ansicht = new GespeicherteAnsicht { Id = Guid.NewGuid(), BenutzerId = benutzerId, Name = name };
            db.GespeicherteAnsichten.Add(ansicht);
        }

        ansicht.Definition = JsonSerializer.Serialize(
            new Definition(eingabe.Filter ?? new BelegFilterDto(), eingabe.Spalten ?? [], eingabe.Darstellung), Json);
        await db.SaveChangesAsync(ct);
        return Dto(ansicht);
    }

    /// <returns><c>false</c>, wenn der Benutzer keine Ansicht mit dieser ID hat.</returns>
    public async Task<bool> LoeschenAsync(Guid benutzerId, Guid id, CancellationToken ct) =>
        await db.GespeicherteAnsichten.Where(a => a.Id == id && a.BenutzerId == benutzerId).ExecuteDeleteAsync(ct) > 0;

    /// <summary>Fehler je Feld; leer, wenn die Eingabe gültig ist.</summary>
    public static Dictionary<string, string[]> Pruefen(GespeicherteAnsichtAnlegenDto? eingabe)
    {
        Dictionary<string, string[]> fehler = [];
        if (eingabe is null)
        {
            fehler[""] = ["Die Anfrage enthält keine Ansicht."];
            return fehler;
        }

        var name = eingabe.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            fehler["name"] = ["Der Name darf nicht leer sein."];
        }
        else if (name.Length > MaxNameLaenge)
        {
            fehler["name"] = [$"Der Name darf höchstens {MaxNameLaenge} Zeichen lang sein."];
        }

        if (!Darstellungen.Contains(eingabe.Darstellung))
        {
            fehler["darstellung"] = ["Erlaubt sind „Tabelle“, „KleineKarten“ und „GrosseKarten“."];
        }

        return fehler;
    }

    private static GespeicherteAnsichtDto Dto(GespeicherteAnsicht ansicht)
    {
        var definition = Lesen(ansicht.Definition);
        return new GespeicherteAnsichtDto(ansicht.Id, ansicht.Name, definition.Filter, definition.Spalten, definition.Darstellung);
    }

    private static Definition Lesen(string json)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<Definition>(json, Json);
            return new Definition(
                definition?.Filter ?? new BelegFilterDto(),
                definition?.Spalten ?? [],
                definition?.Darstellung ?? Darstellungen[0]);
        }
        catch (JsonException)
        {
            return new Definition(new BelegFilterDto(), [], Darstellungen[0]);
        }
    }

    private sealed record Definition(BelegFilterDto Filter, string[] Spalten, string Darstellung);
}
