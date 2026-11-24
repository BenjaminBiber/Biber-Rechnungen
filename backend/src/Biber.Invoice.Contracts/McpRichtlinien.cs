namespace Biber.Invoice.Contracts;

/// <summary>Autorisierungsrichtlinien für <c>/mcp</c>: verlangen ein API-Token mit dem jeweiligen Scope.</summary>
public static class McpRichtlinien
{
    public const string McpLesen = "McpLesen";
    public const string McpUpload = "McpUpload";
}
