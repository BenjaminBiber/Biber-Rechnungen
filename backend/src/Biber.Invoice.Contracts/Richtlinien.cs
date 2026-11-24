namespace Biber.Invoice.Contracts;

/// <summary>Namen der Autorisierungsrichtlinien. Jeder Endpunkt setzt genau eine davon.</summary>
public static class Richtlinien
{
    /// <summary>Rollen Admin, Benutzer oder NurLesen.</summary>
    public const string Lesen = "Lesen";

    /// <summary>Rollen Admin oder Benutzer.</summary>
    public const string Bearbeiten = "Bearbeiten";

    /// <summary>Nur Rolle Admin.</summary>
    public const string Admin = "Admin";
}
