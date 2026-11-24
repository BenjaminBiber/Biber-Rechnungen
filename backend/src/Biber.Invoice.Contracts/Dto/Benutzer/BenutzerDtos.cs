using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Benutzer;

public sealed record BenutzerAnlegenDto(string Name, string Email, string Passwort, Rolle Rolle);

public sealed record BenutzerAendernDto(Rolle Rolle, bool Aktiv);

public sealed record NeuesPasswortDto(string NeuesPasswort);

public sealed record BenutzerFreigebenDto(Rolle Rolle);
