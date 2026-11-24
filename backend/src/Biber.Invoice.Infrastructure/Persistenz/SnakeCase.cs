using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Persistenz;

/// <summary>
/// Benennt Tabellen, Spalten, Schlüssel, Fremdschlüssel und Indizes in snake_case um.
/// Tabellen eigener Entitäten heißen wie die Klasse (<c>AuditEintrag</c> → <c>audit_eintrag</c>),
/// Identity-Tabellen behalten ihren Namen in snake_case (<c>AspNetUsers</c> → <c>asp_net_users</c>).
/// </summary>
internal static class SnakeCase
{
    public static void Anwenden(ModelBuilder modelBuilder)
    {
        var typen = modelBuilder.Model.GetEntityTypes().Where(t => !t.IsMappedToJson()).ToList();

        foreach (var typ in typen.Where(t => !t.IsOwned()))
        {
            var eigene = typ.ClrType.Namespace == typeof(Domain.Entitaeten.Beleg).Namespace;
            typ.SetTableName(Umwandeln(eigene ? typ.ClrType.Name : typ.GetTableName()!));
        }

        foreach (var typ in typen)
        {
            // Spalten eines Owned-Typs tragen den Namen der Navigation als Präfix (matching_muster, anschrift_ort).
            var praefix = typ.IsOwned() ? typ.FindOwnership()!.PrincipalToDependent!.Name + "_" : "";

            // Der Schlüssel eines Owned-Typs teilt sich die Spalte mit dem Schlüssel des Besitzers.
            foreach (var eigenschaft in typ.GetDeclaredProperties().Where(e => !(typ.IsOwned() && e.IsPrimaryKey())))
            {
                eigenschaft.SetColumnName(Umwandeln(praefix + eigenschaft.GetColumnName()));
            }
        }

        foreach (var typ in typen)
        {
            foreach (var schluessel in typ.GetDeclaredKeys())
            {
                schluessel.SetName(Umwandeln(schluessel.GetName()!));
            }

            foreach (var fremdschluessel in typ.GetDeclaredForeignKeys())
            {
                fremdschluessel.SetConstraintName(Umwandeln(fremdschluessel.GetConstraintName()!));
            }

            foreach (var index in typ.GetDeclaredIndexes())
            {
                index.SetDatabaseName(Umwandeln(index.GetDatabaseName()!));
            }
        }
    }

    /// <summary><c>DiKonfidenz</c> → <c>di_konfidenz</c>, <c>Matching_Muster</c> → <c>matching_muster</c>.</summary>
    public static string Umwandeln(string name)
    {
        var ergebnis = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var zeichen = name[i];
            if (char.IsUpper(zeichen))
            {
                var vorher = i > 0 ? name[i - 1] : '_';
                var danach = i + 1 < name.Length ? name[i + 1] : '_';
                if (vorher != '_' && (char.IsLower(vorher) || char.IsDigit(vorher) || char.IsLower(danach)))
                {
                    ergebnis.Append('_');
                }

                ergebnis.Append(char.ToLowerInvariant(zeichen));
            }
            else
            {
                ergebnis.Append(zeichen);
            }
        }

        return ergebnis.ToString();
    }
}
