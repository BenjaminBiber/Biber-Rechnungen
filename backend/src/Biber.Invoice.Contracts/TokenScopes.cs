namespace Biber.Invoice.Contracts;

/// <summary>Berechtigungen eines API-Tokens (Claim <c>scope</c>).</summary>
public static class TokenScopes
{
    public const string Lesen = "lesen";
    public const string Upload = "upload";

    public static readonly IReadOnlyList<string> Alle = [Lesen, Upload];
}
