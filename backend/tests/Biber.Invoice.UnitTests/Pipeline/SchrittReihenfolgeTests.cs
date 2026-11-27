using System.Reflection;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class SchrittReihenfolgeTests
{
    private static int[] Werte() =>
        typeof(SchrittReihenfolge)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (int)f.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void Konstanten_sind_eindeutig()
    {
        var werte = Werte();

        werte.Length.ShouldBe(17);
        werte.Distinct().Count().ShouldBe(werte.Length);
    }

    [Fact]
    public void Konstanten_steigen_in_Deklarationsreihenfolge_auf()
    {
        Werte().ShouldBeInOrder(SortDirection.Ascending);
    }

    [Fact]
    public void Pipeline_beginnt_mit_Hash_Dublette_und_endet_mit_Ablage()
    {
        var werte = Werte();

        werte.Min().ShouldBe(SchrittReihenfolge.HashDublette);
        werte.Max().ShouldBe(SchrittReihenfolge.Ablage);
    }
}
