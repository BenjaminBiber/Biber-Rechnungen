using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Auth;

public enum AnmeldeStatus
{
    Ok,
    TotpErforderlich,
    WartetAufFreigabe,
    Gesperrt,
    Ungueltig,
}

public sealed record AuthStatusDto(
    bool Eingerichtet,
    bool Angemeldet,
    BenutzerDto? Benutzer,
    bool RegistrierungOffen,
    bool GoogleAktiv);

public sealed record TotpEinrichtungDto(string Schluessel, string OtpAuthUri);

public sealed record ErsteinrichtungDto(string Name, string Email, string Passwort, string TotpSchluessel);

public sealed record AnmeldenDto(string Email, string Passwort, bool AngemeldetBleiben);

public sealed record AnmeldeErgebnisDto(AnmeldeStatus Status);

public sealed record TotpDto(string Code, bool GeraetVertrauen);

/// <summary>Ein einzelner Code: TOTP-Code beim Aktivieren oder Wiederherstellungscode beim Anmelden.</summary>
public sealed record CodeDto(string Code);

public sealed record RegistrierenDto(string Name, string Email, string Passwort);

/// <summary>Ein Benutzerkonto. <see cref="Rolle"/> ist <c>null</c>, solange das Konto auf Freigabe wartet.</summary>
public sealed record BenutzerDto(
    Guid Id,
    string Name,
    string Email,
    Rolle? Rolle,
    bool Freigegeben,
    bool TotpAktiv,
    string Anmeldung,
    DateTimeOffset? ZuletztAktiv);
