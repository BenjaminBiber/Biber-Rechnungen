namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>Einstellbare Uhr für Tests. Startet am 15.01.2026, 10:00 UTC.</summary>
public sealed class FesteZeit : TimeProvider
{
    private DateTimeOffset _jetzt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _jetzt;

    public void Setze(DateTimeOffset jetzt) => _jetzt = jetzt.ToUniversalTime();

    public void Vorspulen(TimeSpan dauer) => _jetzt += dauer;
}
