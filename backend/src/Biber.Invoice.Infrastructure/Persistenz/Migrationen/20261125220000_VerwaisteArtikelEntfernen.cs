using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <summary>
/// Reine Datenbereinigung ohne Modelländerung: Artikel ohne Position (übrig nach Umbenennen von Positionen)
/// und danach leere Stammartikel löschen. Neue Verwaiste räumt <c>VerwaisteArtikel</c> nach jeder Änderung weg.
/// </summary>
[DbContext(typeof(BiberDbContext))]
[Migration("20261125220000_VerwaisteArtikelEntfernen")]
public partial class VerwaisteArtikelEntfernen : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM artikel a
            WHERE NOT EXISTS (SELECT 1 FROM position p WHERE p.artikel_id = a.id);

            DELETE FROM stammartikel s
            WHERE NOT EXISTS (SELECT 1 FROM artikel a WHERE a.stammartikel_id = s.id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Gelöschte Artikel lassen sich nicht wiederherstellen; sie hatten ohnehin keine Käufe.
    }
}
