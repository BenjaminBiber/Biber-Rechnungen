using System.Collections;
using System.Globalization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Biber.Invoice.Infrastructure.Module.Audit;

/// <summary>
/// Schreibt für jede Änderung an Belegen (mit Positionen, Steuerzeilen, Tags, Dateien, Prüfbefunden) und
/// Stammdaten (Korrespondent, Kategorie, Tag) Einträge ins Änderungsprotokoll – in derselben Speicherung.
/// </summary>
public sealed class AuditInterceptor(
    IAktuellerBenutzer benutzer,
    TimeProvider zeit,
    AuditSchreiber schreiber) : SaveChangesInterceptor
{
    public const string Angelegt = "Angelegt";
    public const string Geaendert = "Geaendert";
    public const string Geloescht = "Geloescht";

    private static readonly HashSet<string> NichtProtokolliert =
    [
        nameof(Beleg.Volltext),
        BelegKonfiguration.Suchvektor,
        nameof(BelegDatei.LetztePruefungAm),
        nameof(BelegDatei.PruefErgebnis),
    ];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } kontext)
        {
            Protokollieren(kontext);
        }

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } kontext)
        {
            Protokollieren(kontext);
        }

        return ValueTask.FromResult(result);
    }

    private void Protokollieren(DbContext kontext)
    {
        kontext.ChangeTracker.DetectChanges();
        var neu = new List<AuditEintrag>();
        var befunde = new Dictionary<(Guid BelegId, string Aktion), int>();

        foreach (var eintrag in kontext.ChangeTracker.Entries().ToList())
        {
            if (eintrag.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (eintrag.Entity)
            {
                case Beleg b:
                    Erfassen(neu, eintrag, nameof(Beleg), b.Id.ToString(), b.Id, null);
                    break;
                case Position p:
                    Erfassen(neu, eintrag, nameof(Position), p.Id.ToString(), p.BelegId, p.Text);
                    break;
                case Steuerzeile s:
                    Erfassen(neu, eintrag, nameof(Steuerzeile), s.Id.ToString(), s.BelegId, Wert(s.Satz));
                    break;
                case BelegTag t when eintrag.State != EntityState.Modified:
                    neu.Add(AnlegenOderLoeschen(eintrag.State, nameof(BelegTag), t.TagId.ToString(), t.BelegId, null,
                        TagName(kontext, t)));
                    break;
                case BelegDatei d when eintrag.State != EntityState.Modified:
                    neu.Add(AnlegenOderLoeschen(eintrag.State, nameof(BelegDatei), d.Id.ToString(), d.BelegId,
                        d.Rolle.ToString(), d.Pfad));
                    break;
                case BelegDatei d:
                    Erfassen(neu, eintrag, nameof(BelegDatei), d.Id.ToString(), d.BelegId, null);
                    break;
                case PruefBefund f when eintrag.State != EntityState.Modified:
                    var schluessel = (f.BelegId, AktionFuer(eintrag.State));
                    befunde[schluessel] = befunde.GetValueOrDefault(schluessel) + 1;
                    break;
                case Korrespondent k:
                    Erfassen(neu, eintrag, nameof(Korrespondent), k.Id.ToString(), null, k.Name);
                    break;
                case Kategorie k:
                    Erfassen(neu, eintrag, nameof(Kategorie), k.Id.ToString(), null, k.Name);
                    break;
                case Tag t:
                    Erfassen(neu, eintrag, nameof(Tag), t.Id.ToString(), null, t.Name);
                    break;
                default:
                    EigentumErfassen(neu, eintrag);
                    break;
            }
        }

        foreach (var ((belegId, aktion), anzahl) in befunde)
        {
            neu.Add(Neu(nameof(PruefBefund), belegId.ToString(), belegId, aktion, null,
                anzahl.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var notiz in schreiber.Entnehmen())
        {
            neu.Add(Neu(notiz.Entitaet, notiz.EntitaetId, notiz.BelegId, notiz.Aktion, null, notiz.Text));
        }

        kontext.AddRange(neu);
    }

    /// <summary>Änderungen an Owned-Typen (Matching, Anschrift) gehören zur besitzenden Stammdaten-Entität.</summary>
    private void EigentumErfassen(List<AuditEintrag> neu, EntityEntry eintrag)
    {
        var eigentum = eintrag.Metadata.FindOwnership();
        if (eigentum is null || eintrag.State != EntityState.Modified)
        {
            return;
        }

        var besitzer = eigentum.PrincipalEntityType.ClrType;
        if (besitzer != typeof(Korrespondent) && besitzer != typeof(Kategorie) && besitzer != typeof(Tag))
        {
            return;
        }

        var id = eintrag.Property(eigentum.Properties[0].Name).CurrentValue?.ToString() ?? "";
        FeldaenderungenErfassen(neu, eintrag, besitzer.Name, id, null, eigentum.PrincipalToDependent?.Name);
    }

    private void Erfassen(List<AuditEintrag> neu, EntityEntry eintrag, string entitaet, string id, Guid? belegId,
        string? bezeichnung)
    {
        if (eintrag.State == EntityState.Modified)
        {
            FeldaenderungenErfassen(neu, eintrag, entitaet, id, belegId, null);
            return;
        }

        neu.Add(AnlegenOderLoeschen(eintrag.State, entitaet, id, belegId, null, bezeichnung));
    }

    /// <summary>Beim Anlegen steht <paramref name="wert"/> im neuen, beim Löschen im alten Wert.</summary>
    private AuditEintrag AnlegenOderLoeschen(EntityState zustand, string entitaet, string id, Guid? belegId,
        string? feld, string? wert) =>
        zustand == EntityState.Added
            ? Neu(entitaet, id, belegId, Angelegt, feld, wert)
            : Neu(entitaet, id, belegId, Geloescht, feld, null, wert);

    private void FeldaenderungenErfassen(List<AuditEintrag> neu, EntityEntry eintrag, string entitaet, string id,
        Guid? belegId, string? praefix)
    {
        foreach (var eigenschaft in eintrag.Properties)
        {
            var name = eigenschaft.Metadata.Name;
            if (!eigenschaft.IsModified || eigenschaft.Metadata.IsPrimaryKey() || NichtProtokolliert.Contains(name))
            {
                continue;
            }

            var alt = Wert(eigenschaft.OriginalValue);
            var neuerWert = Wert(eigenschaft.CurrentValue);
            if (alt == neuerWert)
            {
                continue;
            }

            neu.Add(Neu(entitaet, id, belegId, Geaendert, praefix is null ? name : $"{praefix}.{name}", neuerWert,
                alt));
        }
    }

    private AuditEintrag Neu(string entitaet, string id, Guid? belegId, string aktion, string? feld,
        string? neuerWert, string? alterWert = null) => new()
        {
            Zeitpunkt = zeit.GetUtcNow(),
            BenutzerId = benutzer.IstSystem ? null : benutzer.Id,
            BenutzerName = benutzer.IstSystem ? "System" : benutzer.Anzeigename,
            Entitaet = entitaet,
            EntitaetId = id,
            BelegId = belegId,
            Aktion = aktion,
            Feld = feld,
            AlterWert = alterWert,
            NeuerWert = neuerWert,
        };

    private static string AktionFuer(EntityState zustand) => zustand switch
    {
        EntityState.Added => Angelegt,
        EntityState.Deleted => Geloescht,
        _ => Geaendert,
    };

    private static string? TagName(DbContext kontext, BelegTag belegTag) =>
        belegTag.Tag?.Name
        ?? kontext.Set<Tag>().Local.FirstOrDefault(t => t.Id == belegTag.TagId)?.Name
        ?? kontext.Set<Tag>().AsNoTracking().Where(t => t.Id == belegTag.TagId).Select(t => t.Name).FirstOrDefault();

    /// <summary>Wert als Text: Datum ISO, Zahlen mit Punkt, Verweise als ID, Listen kommagetrennt.</summary>
    internal static string? Wert(object? wert) => wert switch
    {
        null => null,
        string s => s,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        IEnumerable liste => string.Join(", ", liste.Cast<object?>().Select(Wert)),
        _ => wert.ToString(),
    };
}
