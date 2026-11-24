using System.Text.Json;
using System.Text.Json.Serialization;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Api.Module.Auth;

/// <summary>Gemeinsame Bausteine der Endpunkte für Anmeldung, Konto und Benutzerverwaltung.</summary>
internal static class AuthHilfen
{
    public const string AnbieterGoogle = "Google";

    /// <summary>JSON wie im Vertrag: camelCase, Enums als Namen.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IResult Ok<T>(T wert) => Results.Json(wert, Json);

    public static IResult Konflikt(string titel) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, title: titel);

    public static IResult NichtGefunden() => Results.Problem(statusCode: StatusCodes.Status404NotFound);

    public static IResult Ungueltig(string feld, string meldung) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [feld] = [meldung] });

    /// <summary>Fehler von Identity als 400; Passwortfehler unter <paramref name="passwortFeld"/>, der Rest unter „email“.</summary>
    public static IResult Ungueltig(IdentityResult ergebnis, string passwortFeld) =>
        Results.ValidationProblem(ergebnis.Errors
            .GroupBy(f => f.Code.StartsWith("Password", StringComparison.Ordinal) ? passwortFeld : "email")
            .ToDictionary(g => g.Key, g => g.Select(f => f.Description).ToArray()));

    /// <summary>Liest den Body mit <see cref="Json"/>; <c>null</c> bei ungültigem Inhalt.</summary>
    public static async Task<T?> LesenAsync<T>(HttpRequest anfrage, CancellationToken ct)
        where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(anfrage.Body, Json, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Prüft Name und E-Mail; die Fehler stehen danach in <paramref name="fehler"/>.</summary>
    public static void NameUndEmailPruefen(string? name, string? email, Dictionary<string, string[]> fehler)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            fehler["name"] = ["Der Name darf nicht leer sein."];
        }
        else if (name.Length > 200)
        {
            fehler["name"] = ["Der Name darf höchstens 200 Zeichen lang sein."];
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            fehler["email"] = ["Die E-Mail-Adresse darf nicht leer sein."];
        }
    }

    public static Benutzer NeuerBenutzer(string name, string email, bool freigegeben, DateTimeOffset jetzt) => new()
    {
        UserName = email.Trim(),
        Email = email.Trim(),
        EmailConfirmed = true,
        Anzeigename = name.Trim(),
        Freigegeben = freigegeben,
        ErstelltAm = jetzt,
    };

    public static async Task<BenutzerDto> DtoAsync(Benutzer benutzer, UserManager<Benutzer> userManager)
    {
        var rollen = await userManager.GetRolesAsync(benutzer);
        return Dto(benutzer, rollen.FirstOrDefault());
    }

    public static async Task<List<BenutzerDto>> AlleAsync(BiberDbContext db, CancellationToken ct)
    {
        var benutzer = await db.Users.AsNoTracking().OrderBy(b => b.Anzeigename).ThenBy(b => b.Email).ToListAsync(ct);
        var rollen = await (
                from zuordnung in db.UserRoles
                join rolle in db.Roles on zuordnung.RoleId equals rolle.Id
                select new { zuordnung.UserId, rolle.Name })
            .ToListAsync(ct);
        var rolleJeBenutzer = rollen.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First().Name);
        return benutzer.Select(b => Dto(b, rolleJeBenutzer.GetValueOrDefault(b.Id))).ToList();
    }

    /// <summary>Anzahl der aktiven (freigegebenen) Admins.</summary>
    public static Task<int> AktiveAdminsAsync(BiberDbContext db, CancellationToken ct) =>
        (from benutzer in db.Users
         join zuordnung in db.UserRoles on benutzer.Id equals zuordnung.UserId
         where benutzer.Freigegeben && zuordnung.RoleId == SystemIds.RolleAdmin
         select benutzer.Id).Distinct().CountAsync(ct);

    private static BenutzerDto Dto(Benutzer b, string? rolle) => new(
        b.Id,
        b.Anzeigename,
        b.Email ?? "",
        Enum.TryParse<Rolle>(rolle, out var r) ? r : null,
        b.Freigegeben,
        b.TwoFactorEnabled,
        b.PasswordHash is null ? AnbieterGoogle : "Passwort",
        b.LetzteAktivitaetAm);
}
