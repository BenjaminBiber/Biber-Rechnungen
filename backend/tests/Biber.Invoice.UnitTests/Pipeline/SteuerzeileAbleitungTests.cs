using Biber.Invoice.Infrastructure.Module.Pipeline;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class SteuerzeileAbleitungTests
{
    [Theory]
    [InlineData(221.85, 42.15, 264.00, 19)]
    [InlineData(100.00, 7.00, 107.00, 7)]
    [InlineData(100.00, 16.00, 116.00, 16)]
    [InlineData(100.00, 5.00, 105.00, 5)]
    [InlineData(50.00, 0.00, 50.00, 0)]
    [InlineData(10.08, 1.92, 12.00, 19)] // Steuer 1,9152 → 1,92
    [InlineData(10.00, 1.91, 11.91, 19)] // 0,01 € Rundungsdifferenz liegt in der Toleranz
    [InlineData(-84.03, -15.97, -100.00, 19)] // Gutschrift
    public void Leitet_den_Satz_ab(double netto, double steuer, double brutto, double satz) =>
        SteuerzeileAbleitung.Satz((decimal)netto, (decimal)steuer, (decimal)brutto).ShouldBe((decimal)satz);

    [Theory]
    [InlineData(221.85, 42.15, 260.00)] // Netto + USt ≠ Brutto
    [InlineData(100.00, 12.00, 112.00)] // kein üblicher Satz, z. B. gemischte Sätze
    [InlineData(0.00, 0.00, 0.00)]
    public void Leitet_nichts_ab(double netto, double steuer, double brutto) =>
        SteuerzeileAbleitung.Satz((decimal)netto, (decimal)steuer, (decimal)brutto).ShouldBeNull();

    [Fact]
    public void Leitet_ohne_alle_drei_Werte_nichts_ab()
    {
        SteuerzeileAbleitung.Satz(null, 19m, 119m).ShouldBeNull();
        SteuerzeileAbleitung.Satz(100m, null, 119m).ShouldBeNull();
        SteuerzeileAbleitung.Satz(100m, 19m, null).ShouldBeNull();
    }
}
