using System.IO.Compression;
using Biber.Invoice.Infrastructure.Module.Zuordnung;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>Vorhergesagte Ziel-ID mit Wahrscheinlichkeit zwischen 0 und 1.</summary>
public readonly record struct AutoTreffer(Guid Id, double Wahrscheinlichkeit);

/// <summary>Ergebnis aller vorhandenen Modelle für einen Volltext; ohne Modell bzw. bei „keine“ ist der Treffer <c>null</c>.</summary>
public sealed record AutoVorhersage(AutoTreffer? Kategorie, AutoTreffer? Korrespondent, IReadOnlyList<AutoTreffer> Tags);

/// <summary>
/// Hält die Modelle als Singleton im Speicher. Vor jeder Vorhersage wird geprüft, ob in der Datenbank neuere
/// Modelle liegen (nach einem Training); dann werden sie neu geladen. <see cref="PredictionEngine{TSrc,TDst}"/>
/// ist nicht threadsicher – Laden und Vorhersage laufen deshalb unter einer Sperre.
/// </summary>
public sealed class AutoMatchingVorhersage(IServiceScopeFactory scopes) : IDisposable
{
    private readonly SemaphoreSlim _sperre = new(1, 1);
    private readonly MLContext _ml = new(AutoMatchingTrainer.Seed);
    private Stand? _stand;

    /// <returns><c>null</c>, wenn es kein Modell gibt.</returns>
    public async Task<AutoVorhersage?> VorhersagenAsync(string text, CancellationToken ct)
    {
        await _sperre.WaitAsync(ct);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var kennung = string.Join(',', await db.MatchingModelle.OrderBy(m => m.Id).Select(m => m.Id).ToListAsync(ct));
            if (kennung.Length == 0)
            {
                return null;
            }

            if (_stand?.Kennung != kennung)
            {
                var modelle = await db.MatchingModelle.AsNoTracking().ToListAsync(ct);
                _stand?.Dispose();
                _stand = null;
                _stand = new Stand(
                    kennung,
                    Mehrklassen(modelle.FirstOrDefault(m => m.Ziel == ZuordnungsDienst.ZielKategorie)?.Daten),
                    Mehrklassen(modelle.FirstOrDefault(m => m.Ziel == ZuordnungsDienst.ZielKorrespondent)?.Daten),
                    Tags(modelle.FirstOrDefault(m => m.Ziel == ZuordnungsDienst.ZielTag)?.Daten));
            }

            return _stand.Vorhersagen(MlPipelines.Kuerzen(text));
        }
        finally
        {
            _sperre.Release();
        }
    }

    public void Dispose()
    {
        _stand?.Dispose();
        _sperre.Dispose();
    }

    private PredictionEngine<MehrklassenEingabe, MehrklassenAusgabe>? Mehrklassen(byte[]? daten)
    {
        if (daten is null)
        {
            return null;
        }

        using var strom = new MemoryStream(daten);
        return _ml.Model.CreatePredictionEngine<MehrklassenEingabe, MehrklassenAusgabe>(_ml.Model.Load(strom, out _));
    }

    private Dictionary<Guid, PredictionEngine<BinaerEingabe, BinaerAusgabe>> Tags(byte[]? daten)
    {
        var engines = new Dictionary<Guid, PredictionEngine<BinaerEingabe, BinaerAusgabe>>();
        if (daten is null)
        {
            return engines;
        }

        using var archiv = new ZipArchive(new MemoryStream(daten), ZipArchiveMode.Read);
        foreach (var eintrag in archiv.Entries)
        {
            // ML.NET braucht einen durchsuchbaren Strom; der Eintrag wird deshalb zuerst kopiert.
            using var strom = new MemoryStream();
            using (var quelle = eintrag.Open())
            {
                quelle.CopyTo(strom);
            }

            strom.Position = 0;
            engines[Guid.Parse(eintrag.Name)] =
                _ml.Model.CreatePredictionEngine<BinaerEingabe, BinaerAusgabe>(_ml.Model.Load(strom, out _));
        }

        return engines;
    }

    private sealed class Stand(
        string kennung,
        PredictionEngine<MehrklassenEingabe, MehrklassenAusgabe>? kategorie,
        PredictionEngine<MehrklassenEingabe, MehrklassenAusgabe>? korrespondent,
        Dictionary<Guid, PredictionEngine<BinaerEingabe, BinaerAusgabe>> tags) : IDisposable
    {
        public string Kennung { get; } = kennung;

        public AutoVorhersage Vorhersagen(string text) => new(
            Mehrklassen(kategorie, text),
            Mehrklassen(korrespondent, text),
            [.. tags.Select(t => new AutoTreffer(t.Key, t.Value.Predict(new BinaerEingabe { Text = text }).Probability))]);

        public void Dispose()
        {
            kategorie?.Dispose();
            korrespondent?.Dispose();
            foreach (var engine in tags.Values)
            {
                engine.Dispose();
            }
        }

        private static AutoTreffer? Mehrklassen(PredictionEngine<MehrklassenEingabe, MehrklassenAusgabe>? engine, string text)
        {
            if (engine is null)
            {
                return null;
            }

            var ausgabe = engine.Predict(new MehrklassenEingabe { Text = text });
            return Guid.TryParse(ausgabe.PredictedLabel, out var id) ? new AutoTreffer(id, ausgabe.Score.Max()) : null;
        }
    }
}
