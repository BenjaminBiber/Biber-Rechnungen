using Microsoft.ML;
using Microsoft.ML.Transforms.Text;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>Eingabe der Mehrklassen-Modelle (Kategorie, Korrespondent); <see cref="Klasse"/> ist die Ziel-ID oder „keine“.</summary>
public sealed class MehrklassenEingabe
{
    public string Text { get; set; } = "";
    public string Klasse { get; set; } = "";
}

public sealed class MehrklassenAusgabe
{
    public string Klasse { get; set; } = "";
    public string PredictedLabel { get; set; } = "";
    public float[] Score { get; set; } = [];
}

/// <summary>Eingabe eines Tag-Modells; <see cref="Label"/> = Tag gesetzt.</summary>
public sealed class BinaerEingabe
{
    public string Text { get; set; } = "";
    public bool Label { get; set; }
}

public sealed class BinaerAusgabe
{
    public bool Label { get; set; }
    public bool PredictedLabel { get; set; }
    public float Probability { get; set; }
}

/// <summary>Gemeinsame Pipelines: Merkmal ist der Volltext als Wort- und Zeichen-N-Gramme.</summary>
internal static class MlPipelines
{
    public const string KeineKlasse = "keine";

    /// <summary>Längere Volltexte werden für Training und Vorhersage gekürzt.</summary>
    public const int MaxTextLaenge = 20_000;

    /// <summary>
    /// L2-Regularisierung der SDCA-Trainer. Ohne sie werden die Wahrscheinlichkeiten bei klar trennbaren Daten
    /// extrem, und schon ein einzelnes gemeinsames Wort hebt einen fremden Text über die Schwellen.
    /// </summary>
    private const float L2 = 0.01f;

    public static string Kuerzen(string text) => text.Length <= MaxTextLaenge ? text : text[..MaxTextLaenge];

    public static IEstimator<ITransformer> Mehrklassen(MLContext ml) =>
        ml.Transforms.Conversion.MapValueToKey("Label", nameof(MehrklassenEingabe.Klasse))
            .Append(Merkmale(ml))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(l2Regularization: L2))
            .Append(ml.Transforms.Conversion.MapKeyToValue(nameof(MehrklassenAusgabe.PredictedLabel)));

    public static IEstimator<ITransformer> Binaer(MLContext ml) =>
        Merkmale(ml).Append(ml.BinaryClassification.Trainers.SdcaLogisticRegression(l2Regularization: L2));

    private static TextFeaturizingEstimator Merkmale(MLContext ml) =>
        ml.Transforms.Text.FeaturizeText(
            "Features",
            new TextFeaturizingEstimator.Options
            {
                WordFeatureExtractor = new WordBagEstimator.Options { NgramLength = 2, UseAllLengths = true },
                CharFeatureExtractor = new WordBagEstimator.Options { NgramLength = 3, UseAllLengths = false },
            },
            nameof(MehrklassenEingabe.Text));
}
