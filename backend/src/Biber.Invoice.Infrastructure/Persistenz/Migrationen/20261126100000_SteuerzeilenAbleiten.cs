using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Biber.Invoice.Infrastructure.Persistenz.Migrationen;

/// <summary>
/// Reine Datenergänzung ohne Modelländerung: Belege ohne Steuerzeile, deren Netto + USt = Brutto ergibt und deren USt
/// zu einem üblichen Satz passt, bekommen eine Steuerzeile – wie es <c>SteuerzeileAbleitung</c> für neue Belege beim
/// Übernehmen der Extraktion tut (gleiche Sätze, Reihenfolge und Toleranz 0,02 €).
/// </summary>
[DbContext(typeof(BiberDbContext))]
[Migration("20261126100000_SteuerzeilenAbleiten")]
public partial class SteuerzeilenAbleiten : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO steuerzeile (id, beleg_id, satz, netto, steuer)
            SELECT gen_random_uuid(), b.id, s.satz, b.netto, b.steuer
            FROM beleg b
            CROSS JOIN LATERAL (
                SELECT v.satz
                FROM (VALUES (19.00, 1), (7.00, 2), (16.00, 3), (5.00, 4), (0.00, 5)) AS v(satz, rang)
                WHERE abs(round(b.netto * v.satz / 100, 2) - b.steuer) <= 0.02
                ORDER BY v.rang
                LIMIT 1
            ) s
            WHERE b.netto IS NOT NULL AND b.steuer IS NOT NULL AND b.brutto IS NOT NULL
              AND b.netto <> 0
              AND abs(b.netto + b.steuer - b.brutto) <= 0.02
              AND NOT EXISTS (SELECT 1 FROM steuerzeile z WHERE z.beleg_id = b.id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Abgeleitete Zeilen sind von ausgelesenen nicht mehr zu unterscheiden; sie bleiben stehen.
    }
}
