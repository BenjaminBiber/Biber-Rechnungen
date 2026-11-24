namespace Biber.Invoice.Contracts;

/// <summary>Namen der Authentifizierungsschemata.</summary>
public static class AuthSchemata
{
    /// <summary>Policy-Scheme, das je nach Anfrage an <see cref="Cookie"/> oder <see cref="ApiToken"/> weiterleitet.</summary>
    public const string Standard = "Biber";

    /// <summary>Cookie-Schema von ASP.NET Core Identity.</summary>
    public const string Cookie = "Identity.Application";

    /// <summary>Persönliche API-Tokens (Header <c>Authorization: Bearer …</c>).</summary>
    public const string ApiToken = "ApiToken";
}
