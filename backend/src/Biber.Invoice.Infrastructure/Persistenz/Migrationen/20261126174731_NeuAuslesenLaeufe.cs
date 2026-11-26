using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class NeuAuslesenLaeufe : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "neu_auslesen_lauf",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                gestartet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                aktualisiert_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                beendet_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                gesamt = table.Column<int>(type: "integer", nullable: false),
                verarbeitet = table.Column<int>(type: "integer", nullable: false),
                anzahl_fehler = table.Column<int>(type: "integer", nullable: false),
                anzahl_uebersprungen = table.Column<int>(type: "integer", nullable: false),
                abbruch_angefordert = table.Column<bool>(type: "boolean", nullable: false),
                abbruchgrund = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                letzter_fehler = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                fehler_belege = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_neu_auslesen_lauf", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_neu_auslesen_lauf_gestartet_am",
            table: "neu_auslesen_lauf",
            column: "gestartet_am");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "neu_auslesen_lauf");
    }
}
