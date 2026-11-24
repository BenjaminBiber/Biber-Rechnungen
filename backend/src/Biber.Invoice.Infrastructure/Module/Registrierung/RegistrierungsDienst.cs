using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Registrierung;

/// <summary>Ausgang einer Registrierung; nach außen sehen <see cref="Angenommen"/> und <see cref="Ignoriert"/> gleich aus (202).</summary>
public enum RegistrierungsErgebnis
{
    Angenommen,

    /// <summary>E-Mail-Adresse ist schon vergeben: nichts passiert, damit sich vorhandene Adressen nicht erraten lassen.</summary>
    Ignoriert,

    Geschlossen,
    KeinBenutzer,
    Ungueltig,
}

public enum FreigabeErgebnis
{
    Ok,
    NichtGefunden,
    BereitsFreigegeben,
    UngueltigeRolle,
}

/// <summary>
/// Selbstregistrierung (Konto bleibt gesperrt), Freigabe und Ablehnung durch den Admin. Freigabe und Ablehnung
/// schreiben einen <see cref="AuditEintrag"/> (Entität <see cref="AuditEntitaet"/>).
/// </summary>
public sealed class RegistrierungsDienst(
    BiberDbContext db,
    UserManager<Benutzer> userManager,
    IEinstellungen einstellungen,
    IBenachrichtigungsDienst benachrichtigungen,
    IAktuellerBenutzer aktuellerBenutzer,
    TimeProvider zeit)
{
    public const string AuditEntitaet = "Benutzer";
    public const string AktionFreigegeben = "Freigegeben";
    public const string AktionAbgelehnt = "Abgelehnt";

    public static string EntdoppelungsSchluessel(Guid benutzerId) => $"registrierung-{benutzerId}";

    /// <param name="fehler">Feldfehler bei <see cref="RegistrierungsErgebnis.Ungueltig"/>.</param>
    public async Task<RegistrierungsErgebnis> RegistrierenAsync(
        RegistrierenDto dto, Dictionary<string, string[]> fehler, CancellationToken ct)
    {
        if (!(await einstellungen.LesenAsync<ZugriffEinstellungen>(ct)).OffeneRegistrierung)
        {
            return RegistrierungsErgebnis.Geschlossen;
        }

        if (!await db.Users.AnyAsync(ct))
        {
            return RegistrierungsErgebnis.KeinBenutzer;
        }

        NameUndEmailPruefen(dto, fehler);
        var benutzer = NeuerBenutzer(dto);
        if (await PasswortFehlerAsync(benutzer, dto.Passwort, fehler) || fehler.Count > 0)
        {
            return RegistrierungsErgebnis.Ungueltig;
        }

        var ergebnis = await userManager.CreateAsync(benutzer, dto.Passwort);
        if (!ergebnis.Succeeded)
        {
            if (ergebnis.Errors.All(f => f.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return RegistrierungsErgebnis.Ignoriert;
            }

            fehler["email"] = [.. ergebnis.Errors.Select(f => f.Description)];
            return RegistrierungsErgebnis.Ungueltig;
        }

        await benachrichtigungen.MeldenAsync(
            new NeueBenachrichtigung(
                BenachrichtigungArt.KontoWartetAufFreigabe,
                Schweregrad.Info,
                "Neues Konto wartet auf Freigabe",
                $"{benutzer.Anzeigename} hat sich registriert.",
                "/einstellungen/benutzer",
                NurAdmins: true,
                EntdoppelungsSchluessel: EntdoppelungsSchluessel(benutzer.Id)),
            ct);
        return RegistrierungsErgebnis.Angenommen;
    }

    public async Task<(FreigabeErgebnis Ergebnis, Benutzer? Benutzer)> FreigebenAsync(
        Guid id, Rolle rolle, CancellationToken ct)
    {
        if (!Enum.IsDefined(rolle))
        {
            return (FreigabeErgebnis.UngueltigeRolle, null);
        }

        var benutzer = await userManager.FindByIdAsync(id.ToString());
        if (benutzer is null)
        {
            return (FreigabeErgebnis.NichtGefunden, null);
        }

        if (benutzer.Freigegeben)
        {
            return (FreigabeErgebnis.BereitsFreigegeben, benutzer);
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        await userManager.RemoveFromRolesAsync(benutzer, await userManager.GetRolesAsync(benutzer));
        await userManager.AddToRoleAsync(benutzer, rolle.ToString());
        benutzer.Freigegeben = true;
        await userManager.UpdateAsync(benutzer);
        await userManager.UpdateSecurityStampAsync(benutzer);
        AuditSchreiben(benutzer, AktionFreigegeben, rolle.ToString());
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
        return (FreigabeErgebnis.Ok, benutzer);
    }

    public async Task<FreigabeErgebnis> AblehnenAsync(Guid id, CancellationToken ct)
    {
        var benutzer = await userManager.FindByIdAsync(id.ToString());
        if (benutzer is null)
        {
            return FreigabeErgebnis.NichtGefunden;
        }

        if (benutzer.Freigegeben)
        {
            return FreigabeErgebnis.BereitsFreigegeben;
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        AuditSchreiben(benutzer, AktionAbgelehnt, benutzer.Email);
        await userManager.DeleteAsync(benutzer);
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
        return FreigabeErgebnis.Ok;
    }

    private static void NameUndEmailPruefen(RegistrierenDto dto, Dictionary<string, string[]> fehler)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            fehler["name"] = ["Der Name darf nicht leer sein."];
        }
        else if (dto.Name.Length > 200)
        {
            fehler["name"] = ["Der Name darf höchstens 200 Zeichen lang sein."];
        }

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            fehler["email"] = ["Die E-Mail-Adresse darf nicht leer sein."];
        }
    }

    private Benutzer NeuerBenutzer(RegistrierenDto dto) => new()
    {
        UserName = (dto.Email ?? "").Trim(),
        Email = (dto.Email ?? "").Trim(),
        EmailConfirmed = true,
        Anzeigename = (dto.Name ?? "").Trim(),
        Freigegeben = false,
        ErstelltAm = zeit.GetUtcNow(),
    };

    /// <summary>
    /// Prüft das Passwort vor dem Anlegen, damit ein zu kurzes Passwort immer 400 liefert – unabhängig davon,
    /// ob die E-Mail-Adresse schon vergeben ist.
    /// </summary>
    private async Task<bool> PasswortFehlerAsync(Benutzer benutzer, string? passwort, Dictionary<string, string[]> fehler)
    {
        var beschreibungen = new List<string>();
        foreach (var pruefer in userManager.PasswordValidators)
        {
            var ergebnis = await pruefer.ValidateAsync(userManager, benutzer, passwort ?? "");
            beschreibungen.AddRange(ergebnis.Errors.Select(f => f.Description));
        }

        if (beschreibungen.Count > 0)
        {
            fehler["passwort"] = [.. beschreibungen];
        }

        return beschreibungen.Count > 0;
    }

    private void AuditSchreiben(Benutzer benutzer, string aktion, string? neuerWert) =>
        db.AuditEintraege.Add(new AuditEintrag
        {
            Zeitpunkt = zeit.GetUtcNow(),
            BenutzerId = aktuellerBenutzer.Id,
            BenutzerName = aktuellerBenutzer.IstSystem || aktuellerBenutzer.Id is null ? "System" : aktuellerBenutzer.Anzeigename,
            Entitaet = AuditEntitaet,
            EntitaetId = benutzer.Id.ToString(),
            Aktion = aktion,
            NeuerWert = neuerWert,
        });
}
