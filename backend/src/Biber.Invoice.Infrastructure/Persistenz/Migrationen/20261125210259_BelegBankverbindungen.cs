using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <inheritdoc />
public partial class BelegBankverbindungen : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "bankverbindungen",
            table: "beleg",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "bankverbindungen",
            table: "beleg");
    }
}
