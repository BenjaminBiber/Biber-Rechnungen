using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class Bankleitzahlen : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bank",
            columns: table => new
            {
                blz = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bank", x => x.blz);
            });

        migrationBuilder.CreateTable(
            name: "bankleitzahlen_stand",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                anzahl = table.Column<int>(type: "integer", nullable: false),
                geladen_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                quelle = table.Column<string>(type: "text", nullable: true),
                datei = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                letzter_abruf_am = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                letzter_fehler = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_bankleitzahlen_stand", x => x.id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "bank");

        migrationBuilder.DropTable(
            name: "bankleitzahlen_stand");
    }
}
