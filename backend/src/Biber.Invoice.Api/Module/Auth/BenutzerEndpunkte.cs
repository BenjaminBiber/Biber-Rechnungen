using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Benutzer;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Auth;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Api.Module.Auth;

/// <summary>
/// Benutzerverwaltung durch den Admin: Liste, anlegen (sofort freigegeben), Rolle und Aktiv-Status ändern,
/// Passwort setzen. Aktiv entspricht <see cref="Benutzer.Freigegeben"/>. Der letzte aktive Admin kann weder
/// herabgestuft noch deaktiviert werden (409).
/// </summary>
public sealed class BenutzerEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Benutzer.Liste, ListeAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Benutzer.Liste, AnlegenAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPut(ApiRouten.Benutzer.Einzeln, AendernAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Benutzer.Passwort, PasswortSetzenAsync).RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> ListeAsync(BiberDbContext db, CancellationToken ct) =>
        AuthHilfen.Ok(await AuthHilfen.AlleAsync(db, ct));

    private static async Task<IResult> AnlegenAsync(
        HttpRequest anfrage,
        BiberDbContext db,
        UserManager<Benutzer> userManager,
        TimeProvider zeit,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<BenutzerAnlegenDto>(anfrage, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var fehler = new Dictionary<string, string[]>();
        AuthHilfen.NameUndEmailPruefen(dto.Name, dto.Email, fehler);
        if (!Enum.IsDefined(dto.Rolle))
        {
            fehler["rolle"] = ["Unbekannte Rolle."];
        }

        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var benutzer = AuthHilfen.NeuerBenutzer(dto.Name, dto.Email, freigegeben: true, zeit.GetUtcNow());
        var ergebnis = await userManager.CreateAsync(benutzer, dto.Passwort ?? "");
        if (!ergebnis.Succeeded)
        {
            return AuthHilfen.Ungueltig(ergebnis, "passwort");
        }

        await userManager.AddToRoleAsync(benutzer, dto.Rolle.ToString());
        await speicher.PasswortGeaendertAsync(benutzer.Id, ct);
        await transaktion.CommitAsync(ct);

        return Results.Json(
            await AuthHilfen.DtoAsync(benutzer, userManager),
            AuthHilfen.Json,
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> AendernAsync(
        Guid id,
        HttpRequest anfrage,
        BiberDbContext db,
        UserManager<Benutzer> userManager,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<BenutzerAendernDto>(anfrage, ct);
        if (dto is null || !Enum.IsDefined(dto.Rolle))
        {
            return AuthHilfen.Ungueltig("rolle", "Rolle und Aktiv-Status sind nötig.");
        }

        var benutzer = await userManager.FindByIdAsync(id.ToString());
        if (benutzer is null)
        {
            return AuthHilfen.NichtGefunden();
        }

        var bisherigeRollen = await userManager.GetRolesAsync(benutzer);
        var istAktiverAdmin = benutzer.Freigegeben && bisherigeRollen.Contains(nameof(Rolle.Admin));
        var bleibtAktiverAdmin = dto.Aktiv && dto.Rolle == Rolle.Admin;
        if (istAktiverAdmin && !bleibtAktiverAdmin && await AuthHilfen.AktiveAdminsAsync(db, ct) <= 1)
        {
            return AuthHilfen.Konflikt("Der letzte aktive Admin kann weder herabgestuft noch deaktiviert werden.");
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        var neueRolle = dto.Rolle.ToString();
        if (bisherigeRollen.Count != 1 || bisherigeRollen[0] != neueRolle)
        {
            await userManager.RemoveFromRolesAsync(benutzer, bisherigeRollen);
            await userManager.AddToRoleAsync(benutzer, neueRolle);
            await userManager.UpdateSecurityStampAsync(benutzer);
        }

        if (benutzer.Freigegeben != dto.Aktiv)
        {
            benutzer.Freigegeben = dto.Aktiv;
            await userManager.UpdateSecurityStampAsync(benutzer);
            if (!dto.Aktiv)
            {
                await speicher.AlleSitzungenBeendenAsync(benutzer.Id, ct);
            }
        }

        await transaktion.CommitAsync(ct);
        return AuthHilfen.Ok(await AuthHilfen.DtoAsync(benutzer, userManager));
    }

    private static async Task<IResult> PasswortSetzenAsync(
        Guid id,
        HttpRequest anfrage,
        UserManager<Benutzer> userManager,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<NeuesPasswortDto>(anfrage, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var benutzer = await userManager.FindByIdAsync(id.ToString());
        if (benutzer is null)
        {
            return AuthHilfen.NichtGefunden();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(benutzer);
        var ergebnis = await userManager.ResetPasswordAsync(benutzer, token, dto.NeuesPasswort ?? "");
        if (!ergebnis.Succeeded)
        {
            return AuthHilfen.Ungueltig(ergebnis, "neuesPasswort");
        }

        await userManager.SetLockoutEndDateAsync(benutzer, null);
        await userManager.ResetAccessFailedCountAsync(benutzer);
        await speicher.PasswortGeaendertAsync(benutzer.Id, ct);
        await speicher.AlleSitzungenBeendenAsync(benutzer.Id, ct);
        return Results.NoContent();
    }
}
