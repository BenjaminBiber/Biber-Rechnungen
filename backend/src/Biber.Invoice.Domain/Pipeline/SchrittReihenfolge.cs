namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Position der Pipeline-Schritte; der Orchestrator sortiert aufsteigend.</summary>
public static class SchrittReihenfolge
{
    public const int HashDublette = 100;
    public const int Formaterkennung = 200;
    public const int ERechnung = 300;
    public const int Kosit = 310;
    public const int Belegpruefung = 315;
    public const int DocumentIntelligence = 320;
    public const int BelegpruefungNachAuslesen = 325;
    public const int ExtraktionUebernehmen = 330;
    public const int Vorschau = 350;
    public const int Korrespondent = 400;
    public const int Positionen = 500;
    public const int FachlicheDublette = 600;
    public const int GutschriftBezug = 650;
    public const int Zuordnung = 700;
    public const int AutoZuordnung = 710;
    public const int Pruefungen = 800;
    public const int Ablage = 900;
}
