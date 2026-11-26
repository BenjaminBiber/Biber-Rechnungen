using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class Unterkategorien : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "eltern_id",
            table: "kategorie",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_kategorie_eltern_id",
            table: "kategorie",
            column: "eltern_id");

        migrationBuilder.AddForeignKey(
            name: "fk_kategorie_kategorie_eltern_id",
            table: "kategorie",
            column: "eltern_id",
            principalTable: "kategorie",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_kategorie_kategorie_eltern_id",
            table: "kategorie");

        migrationBuilder.DropIndex(
            name: "ix_kategorie_eltern_id",
            table: "kategorie");

        migrationBuilder.DropColumn(
            name: "eltern_id",
            table: "kategorie");
    }
}
