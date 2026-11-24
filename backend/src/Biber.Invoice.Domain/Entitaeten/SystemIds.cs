namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Feste IDs der Datensätze, die die Initial-Migration anlegt.</summary>
public static class SystemIds
{
    public static readonly Guid RolleAdmin = new("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid RolleBenutzer = new("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid RolleNurLesen = new("a0000000-0000-0000-0000-000000000003");

    /// <summary>Tag „zu prüfen“ (orange, System-Tag).</summary>
    public static readonly Guid TagZuPruefen = new("b0000000-0000-0000-0000-000000000001");

    /// <summary>Tag „Inbox“ (blau, Inbox-Tag).</summary>
    public static readonly Guid TagInbox = new("b0000000-0000-0000-0000-000000000002");
}
