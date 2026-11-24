using Biber.Invoice.Infrastructure.Module.TagsKategorien;

namespace Biber.Invoice.Api.Module.TagsKategorien;

/// <summary>Übersetzt <see cref="Fehler"/> in ProblemDetails.</summary>
internal static class FehlerAntwort
{
    public static IResult Von(Fehler fehler) => fehler.Feld is { } feld
        ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { [feld] = [fehler.Meldung] })
        : TypedResults.Problem(detail: fehler.Meldung, statusCode: fehler.Status);

    public static IResult Oder<T>(Ergebnis<T> ergebnis, Func<T, IResult> erfolg) =>
        ergebnis.Fehler is { } fehler ? Von(fehler) : erfolg(ergebnis.Wert!);
}
