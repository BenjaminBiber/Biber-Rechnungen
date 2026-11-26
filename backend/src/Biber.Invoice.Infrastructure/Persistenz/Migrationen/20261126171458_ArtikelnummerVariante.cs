using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <summary>
/// Artikelnummer und Variante an der Position, Artikelnummer am Artikel. Ein Artikel ist je Korrespondent über die
/// Nummer eindeutig, ohne Nummer weiter über den normalisierten Namen. Bestehende Artikel bleiben ohne Nummer;
/// „Erneut auslesen“ ordnet die Positionen eines Belegs den Artikeln mit Nummer zu.
/// </summary>
public partial class ArtikelnummerVariante : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_artikel_korrespondent_id_name_normalisiert",
            table: "artikel");

        migrationBuilder.AddColumn<string>(
            name: "artikelnummer",
            table: "position",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "variante",
            table: "position",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "artikelnummer",
            table: "artikel",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_artikel_korrespondent_id_artikelnummer",
            table: "artikel",
            columns: new[] { "korrespondent_id", "artikelnummer" },
            unique: true,
            filter: "artikelnummer IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_artikel_korrespondent_id_name_normalisiert",
            table: "artikel",
            columns: new[] { "korrespondent_id", "name_normalisiert" },
            unique: true,
            filter: "artikelnummer IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_artikel_korrespondent_id_artikelnummer",
            table: "artikel");

        migrationBuilder.DropIndex(
            name: "ix_artikel_korrespondent_id_name_normalisiert",
            table: "artikel");

        migrationBuilder.DropColumn(
            name: "artikelnummer",
            table: "position");

        migrationBuilder.DropColumn(
            name: "variante",
            table: "position");

        migrationBuilder.DropColumn(
            name: "artikelnummer",
            table: "artikel");

        // Schlägt fehl, wenn es inzwischen gleichnamige Artikel mit verschiedenen Nummern gibt.
        migrationBuilder.CreateIndex(
            name: "ix_artikel_korrespondent_id_name_normalisiert",
            table: "artikel",
            columns: new[] { "korrespondent_id", "name_normalisiert" },
            unique: true);
    }
}
