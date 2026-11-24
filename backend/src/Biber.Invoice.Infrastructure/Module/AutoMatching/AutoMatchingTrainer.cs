using System.IO.Compression;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Zuordnung;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>
/// Trainiert je Zielart ein Modell aus den bestätigten, fertigen Belegen (ohne Dubletten): Kategorie und
/// Korrespondent als Mehrklassen-Klassifikation, Tags als Bündel binärer Klassifikatoren. Nur Ziele mit
/// Algorithmus „Auto“ und genügend Beispielen werden gelernt; je Ziel bleibt nur das neueste Modell.
/// </summary>
public sealed class AutoMatchingTrainer(BiberDbContext db, TimeProvider zeit, ILogger<AutoMatchingTrainer> log)
{
    public const int MindestBelege = 50;
    public const int MindestBeispiele = 5;
    public const int Seed = 42;
    private const double TestAnteil = 0.2;

    private sealed record Beispiel(Guid Id, string Text, Guid? KategorieId, Guid? KorrespondentId, List<Guid> TagIds);

    private sealed record Ergebnis(byte[] Daten, double Guete);

    public async Task TrainierenAsync(CancellationToken ct)
    {
        var beispiele = await db.Belege.AsNoTracking()
            .Where(b => b.Status == BelegStatus.Fertig && b.DuplikatVonId == null && b.Volltext != null && b.Volltext != "")
            .Where(b => !b.Tags.Any(t => t.TagId == SystemIds.TagZuPruefen))
            .OrderBy(b => b.Id)
            .Select(b => new Beispiel(
                b.Id,
                b.Volltext!.Substring(0, MlPipelines.MaxTextLaenge),
                b.KategorieId,
                b.KorrespondentId,
                b.Tags.Select(t => t.TagId).ToList()))
            .ToListAsync(ct);

        if (beispiele.Count < MindestBelege)
        {
            log.LogInformation("Auto-Matching: {Anzahl} Trainingsbelege, mindestens {Mindest} nötig – kein Training.", beispiele.Count, MindestBelege);
            return;
        }

        Mischen(beispiele);

        var kategorien = await db.Kategorien.Where(k => k.Matching.Algorithmus == MatchingAlgorithmus.Auto).Select(k => k.Id).ToListAsync(ct);
        var korrespondenten = await db.Korrespondenten.Where(k => k.Matching.Algorithmus == MatchingAlgorithmus.Auto).Select(k => k.Id).ToListAsync(ct);
        var tags = await db.Tags.Where(t => !t.IstSystem && t.Matching.Algorithmus == MatchingAlgorithmus.Auto).Select(t => t.Id).ToListAsync(ct);

        await SpeichernAsync(ZuordnungsDienst.ZielKategorie, beispiele.Count, () => Mehrklassen(beispiele, b => b.KategorieId, [.. kategorien]), ct);
        await SpeichernAsync(ZuordnungsDienst.ZielKorrespondent, beispiele.Count, () => Mehrklassen(beispiele, b => b.KorrespondentId, [.. korrespondenten]), ct);
        await SpeichernAsync(ZuordnungsDienst.ZielTag, beispiele.Count, () => TagBuendel(beispiele, tags), ct);
    }

    private async Task SpeichernAsync(string ziel, int anzahl, Func<Ergebnis?> trainieren, CancellationToken ct)
    {
        Ergebnis? ergebnis;
        try
        {
            ergebnis = trainieren();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            log.LogWarning(e, "Auto-Matching: Training für {Ziel} fehlgeschlagen.", ziel);
            return;
        }

        if (ergebnis is null)
        {
            log.LogInformation("Auto-Matching: zu wenige Beispiele für {Ziel} – Modell bleibt unverändert.", ziel);
            return;
        }

        var alte = await db.MatchingModelle.Where(m => m.Ziel == ziel).ToListAsync(ct);
        db.MatchingModelle.RemoveRange(alte);
        db.MatchingModelle.Add(new MatchingModell
        {
            Id = Guid.NewGuid(),
            Ziel = ziel,
            Daten = ergebnis.Daten,
            TrainiertAm = zeit.GetUtcNow(),
            AnzahlBeispiele = anzahl,
            Guete = ergebnis.Guete,
        });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Auto-Matching: Modell {Ziel} mit {Anzahl} Belegen trainiert, Güte {Guete:P1}.", ziel, anzahl, ergebnis.Guete);
    }

    private static Ergebnis? Mehrklassen(List<Beispiel> beispiele, Func<Beispiel, Guid?> ziel, HashSet<Guid> autoZiele)
    {
        var klassen = beispiele
            .Select(ziel)
            .OfType<Guid>()
            .Where(autoZiele.Contains)
            .CountBy(id => id)
            .Where(z => z.Value >= MindestBeispiele)
            .Select(z => z.Key)
            .ToHashSet();
        if (klassen.Count == 0)
        {
            return null;
        }

        var zeilen = beispiele
            .Select(b => new MehrklassenEingabe
            {
                Text = b.Text,
                Klasse = ziel(b) is { } id && klassen.Contains(id) ? id.ToString() : MlPipelines.KeineKlasse,
            })
            .ToList();
        if (zeilen.Select(z => z.Klasse).Distinct().Count() < 2)
        {
            return null; // eine einzige Klasse lässt sich nicht unterscheiden
        }

        var (training, test) = Teilen(zeilen);
        var ml = new MLContext(Seed);
        var probe = MlPipelines.Mehrklassen(ml).Fit(ml.Data.LoadFromEnumerable(training));
        var guete = ml.Data
            .CreateEnumerable<MehrklassenAusgabe>(probe.Transform(ml.Data.LoadFromEnumerable(test)), reuseRowObject: false)
            .Average(a => a.PredictedLabel == a.Klasse ? 1.0 : 0.0);

        var alle = ml.Data.LoadFromEnumerable(zeilen);
        return new Ergebnis(Serialisieren(ml, MlPipelines.Mehrklassen(ml).Fit(alle), alle.Schema), guete);
    }

    private static Ergebnis? TagBuendel(List<Beispiel> beispiele, List<Guid> autoTags)
    {
        var gueten = new List<double>();
        using var puffer = new MemoryStream();
        using (var archiv = new ZipArchive(puffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var tagId in autoTags.Order())
            {
                var zeilen = beispiele.Select(b => new BinaerEingabe { Text = b.Text, Label = b.TagIds.Contains(tagId) }).ToList();
                var positive = zeilen.Count(z => z.Label);
                if (positive < MindestBeispiele || positive == zeilen.Count)
                {
                    continue;
                }

                var (training, test) = Teilen(zeilen);
                var ml = new MLContext(Seed);
                var probe = MlPipelines.Binaer(ml).Fit(ml.Data.LoadFromEnumerable(training));
                gueten.Add(ml.Data
                    .CreateEnumerable<BinaerAusgabe>(probe.Transform(ml.Data.LoadFromEnumerable(test)), reuseRowObject: false)
                    .Average(a => a.PredictedLabel == a.Label ? 1.0 : 0.0));

                var alle = ml.Data.LoadFromEnumerable(zeilen);
                var daten = Serialisieren(ml, MlPipelines.Binaer(ml).Fit(alle), alle.Schema);
                using var eintrag = archiv.CreateEntry(tagId.ToString("D")).Open();
                eintrag.Write(daten);
            }
        }

        return gueten.Count == 0 ? null : new Ergebnis(puffer.ToArray(), gueten.Average());
    }

    private static byte[] Serialisieren(MLContext ml, ITransformer modell, DataViewSchema schema)
    {
        using var puffer = new MemoryStream();
        ml.Model.Save(modell, schema, puffer);
        return puffer.ToArray();
    }

    /// <summary>Feste Reihenfolge (nach ID) mit festem Seed gemischt, damit die Aufteilung reproduzierbar ist.</summary>
    private static void Mischen(List<Beispiel> beispiele)
    {
        var zufall = new Random(Seed);
        for (var i = beispiele.Count - 1; i > 0; i--)
        {
            var j = zufall.Next(i + 1);
            (beispiele[i], beispiele[j]) = (beispiele[j], beispiele[i]);
        }
    }

    /// <summary>Die ersten 20 % (nach dem Mischen) werden zurückgehalten und messen die Güte.</summary>
    private static (List<T> Training, List<T> Test) Teilen<T>(List<T> zeilen)
    {
        var anzahlTest = Math.Max(1, (int)Math.Round(zeilen.Count * TestAnteil));
        return (zeilen[anzahlTest..], zeilen[..anzahlTest]);
    }
}
