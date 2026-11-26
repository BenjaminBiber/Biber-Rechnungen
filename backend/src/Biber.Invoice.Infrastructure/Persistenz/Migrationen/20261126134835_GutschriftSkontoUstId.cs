using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class GutschriftSkontoUstId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ust_id_amtliche_anschrift",
            table: "korrespondent",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ust_id_amtlicher_name",
            table: "korrespondent",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ust_id_geprueft_am",
            table: "korrespondent",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ust_id_geprueft_nummer",
            table: "korrespondent",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ust_id_status",
            table: "korrespondent",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Ungeprueft");

        migrationBuilder.AddColumn<Guid>(
            name: "bezug_beleg_id",
            table: "beleg",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "skonto_betrag",
            table: "beleg",
            type: "numeric(14,2)",
            precision: 14,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "skonto_bis",
            table: "beleg",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "skonto_prozent",
            table: "beleg",
            type: "numeric(14,2)",
            precision: 14,
            scale: 2,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_beleg_bezug_beleg_id",
            table: "beleg",
            column: "bezug_beleg_id");

        migrationBuilder.CreateIndex(
            name: "ix_beleg_skonto_bis",
            table: "beleg",
            column: "skonto_bis");

        migrationBuilder.AddForeignKey(
            name: "fk_beleg_beleg_bezug_beleg_id",
            table: "beleg",
            column: "bezug_beleg_id",
            principalTable: "beleg",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_beleg_beleg_bezug_beleg_id",
            table: "beleg");

        migrationBuilder.DropIndex(
            name: "ix_beleg_bezug_beleg_id",
            table: "beleg");

        migrationBuilder.DropIndex(
            name: "ix_beleg_skonto_bis",
            table: "beleg");

        migrationBuilder.DropColumn(
            name: "ust_id_amtliche_anschrift",
            table: "korrespondent");

        migrationBuilder.DropColumn(
            name: "ust_id_amtlicher_name",
            table: "korrespondent");

        migrationBuilder.DropColumn(
            name: "ust_id_geprueft_am",
            table: "korrespondent");

        migrationBuilder.DropColumn(
            name: "ust_id_geprueft_nummer",
            table: "korrespondent");

        migrationBuilder.DropColumn(
            name: "ust_id_status",
            table: "korrespondent");

        migrationBuilder.DropColumn(
            name: "bezug_beleg_id",
            table: "beleg");

        migrationBuilder.DropColumn(
            name: "skonto_betrag",
            table: "beleg");

        migrationBuilder.DropColumn(
            name: "skonto_bis",
            table: "beleg");

        migrationBuilder.DropColumn(
            name: "skonto_prozent",
            table: "beleg");
    }
}
