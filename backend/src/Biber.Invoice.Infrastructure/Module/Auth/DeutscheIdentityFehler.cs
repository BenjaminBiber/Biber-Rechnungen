using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>Deutsche Texte für die Fehler von Identity, die über die API sichtbar werden.</summary>
public sealed class DeutscheIdentityFehler : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        Fehler(nameof(PasswordTooShort), $"Das Passwort muss mindestens {length} Zeichen lang sein.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Fehler(nameof(PasswordRequiresUniqueChars), $"Das Passwort muss mindestens {uniqueChars} verschiedene Zeichen enthalten.");

    public override IdentityError PasswordMismatch() =>
        Fehler(nameof(PasswordMismatch), "Das Passwort ist falsch.");

    public override IdentityError DuplicateEmail(string email) =>
        Fehler(nameof(DuplicateEmail), $"Die E-Mail-Adresse „{email}“ wird bereits verwendet.");

    public override IdentityError DuplicateUserName(string userName) =>
        Fehler(nameof(DuplicateUserName), $"Die E-Mail-Adresse „{userName}“ wird bereits verwendet.");

    public override IdentityError InvalidEmail(string? email) =>
        Fehler(nameof(InvalidEmail), $"„{email}“ ist keine gültige E-Mail-Adresse.");

    public override IdentityError InvalidUserName(string? userName) =>
        Fehler(nameof(InvalidUserName), $"„{userName}“ ist keine gültige E-Mail-Adresse.");

    public override IdentityError InvalidToken() =>
        Fehler(nameof(InvalidToken), "Der Code ist ungültig.");

    private static IdentityError Fehler(string code, string beschreibung) => new() { Code = code, Description = beschreibung };
}
