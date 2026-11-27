using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <summary>
/// Merkmale (Größe, Farbe …) der Position als <c>jsonb</c>-Liste aus Name und Wert statt des Textfelds „Variante“.
/// Vorhandene Texte wie „Größe: 42, Farbe: grau“ werden an „, Name:“ geteilt; Teile ohne „Name:“ (etwa „schwarz / XL“
/// aus Document Intelligence) bleiben ein Merkmal ohne Namen. „Erneut auslesen“ liefert die Namen nach.
/// </summary>
public partial class PositionMerkmale : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "merkmale",
            table: "position",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        // Ein Name beginnt mit einem Buchstaben und endet am ersten Doppelpunkt; „Paketgröße:: 1“ hat einen zu viel.
        migrationBuilder.Sql(
            """
            UPDATE position
            SET merkmale = (
                SELECT coalesce(jsonb_agg(
                    CASE WHEN t.teil ~ '^[[:alpha:]][^:]{0,49}:+\s*\S'
                        THEN jsonb_build_object(
                            'Name', btrim(substring(t.teil FROM '^([^:]+):')),
                            'Wert', btrim(regexp_replace(t.teil, '^[^:]+:+\s*', '')))
                        ELSE jsonb_build_object('Name', NULL, 'Wert', btrim(t.teil))
                    END ORDER BY t.nr), '[]'::jsonb)
                FROM regexp_split_to_table(variante, ',\s+(?=[[:alpha:]][^,:]{0,49}:)') WITH ORDINALITY AS t(teil, nr)
                WHERE btrim(t.teil) <> '')
            WHERE variante IS NOT NULL;
            """);

        migrationBuilder.DropColumn(
            name: "variante",
            table: "position");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "variante",
            table: "position",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE position
            SET variante = left((
                SELECT string_agg(
                    CASE WHEN m.eintrag ->> 'Name' IS NULL
                        THEN m.eintrag ->> 'Wert'
                        ELSE (m.eintrag ->> 'Name') || ': ' || (m.eintrag ->> 'Wert')
                    END, ', ' ORDER BY m.nr)
                FROM jsonb_array_elements(merkmale) WITH ORDINALITY AS m(eintrag, nr)), 100);
            """);

        migrationBuilder.DropColumn(
            name: "merkmale",
            table: "position");
    }
}
