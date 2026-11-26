using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class Bankverbindungen : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "bankverbindungen",
            table: "korrespondent",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        // Vorhandene IBANs in ihrer Reihenfolge übernehmen; ein BIC ist noch nicht bekannt.
        migrationBuilder.Sql(
            """
            UPDATE korrespondent
            SET bankverbindungen = (
                SELECT coalesce(jsonb_agg(jsonb_build_object('Iban', t.iban, 'Bic', NULL) ORDER BY t.nr), '[]'::jsonb)
                FROM unnest(ibans) WITH ORDINALITY AS t(iban, nr));
            """);

        migrationBuilder.DropColumn(
            name: "ibans",
            table: "korrespondent");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<string>>(
            name: "ibans",
            table: "korrespondent",
            type: "text[]",
            nullable: false,
            defaultValueSql: "'{}'::text[]");

        // Die BICs gehen beim Zurückrollen verloren.
        migrationBuilder.Sql(
            """
            UPDATE korrespondent
            SET ibans = ARRAY(
                SELECT t.eintrag ->> 'Iban'
                FROM jsonb_array_elements(bankverbindungen) WITH ORDINALITY AS t(eintrag, nr)
                ORDER BY t.nr);
            """);

        migrationBuilder.DropColumn(
            name: "bankverbindungen",
            table: "korrespondent");
    }
}
