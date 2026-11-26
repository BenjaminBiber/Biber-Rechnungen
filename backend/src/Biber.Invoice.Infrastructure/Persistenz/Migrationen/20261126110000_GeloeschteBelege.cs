using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen
{
    /// <inheritdoc />
    public partial class GeloeschteBelege : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "geloescht_am",
                table: "beleg",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "geloescht_von_id",
                table: "beleg",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "loesch_bemerkung",
                table: "beleg",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "loesch_grund",
                table: "beleg",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_beleg_geloescht_von_id",
                table: "beleg",
                column: "geloescht_von_id");

            migrationBuilder.AddForeignKey(
                name: "fk_beleg_benutzer_geloescht_von_id",
                table: "beleg",
                column: "geloescht_von_id",
                principalTable: "benutzer",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_beleg_benutzer_geloescht_von_id",
                table: "beleg");

            migrationBuilder.DropIndex(
                name: "ix_beleg_geloescht_von_id",
                table: "beleg");

            migrationBuilder.DropColumn(
                name: "geloescht_am",
                table: "beleg");

            migrationBuilder.DropColumn(
                name: "geloescht_von_id",
                table: "beleg");

            migrationBuilder.DropColumn(
                name: "loesch_bemerkung",
                table: "beleg");

            migrationBuilder.DropColumn(
                name: "loesch_grund",
                table: "beleg");
        }
    }
}
