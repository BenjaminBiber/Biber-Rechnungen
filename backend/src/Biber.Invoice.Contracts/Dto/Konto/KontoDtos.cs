namespace Biber.Invoice.Contracts.Dto.Konto;

public sealed record KontoDto(
    string Name,
    string Email,
    DateTimeOffset? PasswortGeaendertAm,
    bool TotpAktiv,
    int Wiederherstellungscodes,
    bool GoogleVerknuepft,
    int AktiveSitzungen);

public sealed record KontoAendernDto(string Name, string Email);

public sealed record PasswortAendernDto(string AltesPasswort, string NeuesPasswort);

public sealed record WiederherstellungscodesDto(string[] Codes);
