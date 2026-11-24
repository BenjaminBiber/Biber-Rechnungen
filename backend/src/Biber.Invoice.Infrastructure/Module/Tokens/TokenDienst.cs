using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Tokens;

/// <summary>Ein gültiges Token samt Benutzer und dessen Rollen.</summary>
public sealed record GeprueftesToken(ApiToken Token, Benutzer Benutzer, IReadOnlyList<string> Rollen);

/// <summary>Ergebnis von <see cref="TokenDienst.ErzeugenAsync"/>; bei Fehler ist <see cref="Feld"/> das betroffene Feld (400) oder <c>null</c> (409).</summary>
public sealed record TokenErzeugung(ApiTokenErstelltDto? Erstellt, string? Feld = null, string? Fehler = null);

/// <summary>
/// Erzeugt, listet, widerruft und prüft persönliche API-Tokens. Gespeichert werden nur SHA-256-Hash und Endung;
/// der Klartext verlässt den Dienst genau einmal in <see cref="ErzeugenAsync"/>.
/// </summary>
public sealed class TokenDienst(BiberDbContext db, TimeProvider zeit)
{
    public const string Praefix = "brk_";
    public const int NameMaxLaenge = 100;
    private const int ZufallsLaenge = 40;
    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private static readonly TimeSpan NutzungsIntervall = TimeSpan.FromMinutes(1);

    public static string HashVon(string klartext) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(klartext)));

    public async Task<IReadOnlyList<ApiTokenDto>> ListeAsync(Guid benutzerId, CancellationToken ct)
    {
        var tokens = await db.Set<ApiToken>().AsNoTracking()
            .Where(t => t.BenutzerId == benutzerId)
            .OrderByDescending(t => t.ErstelltAm)
            .ToListAsync(ct);
        return tokens.Select(AlsDto).ToList();
    }

    public async Task<TokenErzeugung> ErzeugenAsync(Guid benutzerId, ApiTokenAnlegenDto anlegen, CancellationToken ct)
    {
        string name = anlegen.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return new TokenErzeugung(null, "name", "Der Name ist Pflicht.");
        }

        if (name.Length > NameMaxLaenge)
        {
            return new TokenErzeugung(null, "name", $"Der Name darf höchstens {NameMaxLaenge} Zeichen lang sein.");
        }

        string[] scopes = (anlegen.Scopes ?? []).Distinct().ToArray();
        if (scopes.Length == 0 || scopes.Any(s => !TokenScopes.Alle.Contains(s)))
        {
            return new TokenErzeugung(null, "scopes", "Mindestens ein Scope, erlaubt sind „lesen“ und „upload“.");
        }

        bool vergeben = await db.Set<ApiToken>()
            .AnyAsync(t => t.BenutzerId == benutzerId && t.WiderrufenAm == null && t.Name == name, ct);
        if (vergeben)
        {
            return new TokenErzeugung(null, null, "Ein aktives Token mit diesem Namen gibt es bereits.");
        }

        string klartext = Praefix + RandomNumberGenerator.GetString(Base62, ZufallsLaenge);
        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            BenutzerId = benutzerId,
            Name = name,
            Hash = HashVon(klartext),
            Endung = klartext[^4..],
            Scopes = [.. scopes],
            ErstelltAm = zeit.GetUtcNow(),
        };
        db.Add(token);
        await db.SaveChangesAsync(ct);
        return new TokenErzeugung(new ApiTokenErstelltDto(AlsDto(token), klartext));
    }

    /// <summary>Setzt <c>WiderrufenAm</c> (idempotent). <c>false</c>, wenn das Token fehlt oder einem anderen gehört und <paramref name="istAdmin"/> nicht gilt.</summary>
    public async Task<bool> WiderrufenAsync(Guid id, Guid benutzerId, bool istAdmin, CancellationToken ct)
    {
        var token = await db.Set<ApiToken>().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (token is null || (token.BenutzerId != benutzerId && !istAdmin))
        {
            return false;
        }

        if (token.WiderrufenAm is null)
        {
            token.WiderrufenAm = zeit.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    /// <summary>
    /// Liefert das Token zum Klartext, wenn es nicht widerrufen ist und sein Benutzer freigegeben und nicht gesperrt ist;
    /// aktualisiert <c>ZuletztGenutztAm</c> höchstens einmal je Minute.
    /// </summary>
    public async Task<GeprueftesToken?> PruefenAsync(string klartext, CancellationToken ct)
    {
        if (!klartext.StartsWith(Praefix, StringComparison.Ordinal))
        {
            return null;
        }

        string hash = HashVon(klartext);
        var treffer = await db.Set<ApiToken>().AsNoTracking()
            .Where(t => t.Hash == hash && t.WiderrufenAm == null)
            .Join(db.Users, t => t.BenutzerId, b => b.Id, (t, b) => new { Token = t, Benutzer = b })
            .FirstOrDefaultAsync(ct);
        var jetzt = zeit.GetUtcNow();
        if (treffer is null || !treffer.Benutzer.Freigegeben || treffer.Benutzer.LockoutEnd > jetzt)
        {
            return null;
        }

        var rollen = await db.UserRoles
            .Where(ur => ur.UserId == treffer.Benutzer.Id)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
            .ToListAsync(ct);

        if (treffer.Token.ZuletztGenutztAm is not { } zuletzt || jetzt - zuletzt >= NutzungsIntervall)
        {
            await db.Set<ApiToken>()
                .Where(t => t.Id == treffer.Token.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ZuletztGenutztAm, jetzt), ct);
            treffer.Token.ZuletztGenutztAm = jetzt;
        }

        return new GeprueftesToken(treffer.Token, treffer.Benutzer, rollen);
    }

    private static ApiTokenDto AlsDto(ApiToken t) =>
        new(t.Id, t.Name, t.Endung, [.. t.Scopes], t.ErstelltAm, t.ZuletztGenutztAm, t.WiderrufenAm);
}
