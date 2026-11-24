/** Fertiger Befehl zum Einbinden des MCP-Servers in Claude Code (Einstellungen.dc.html → Verbinden). */
export function mcpBefehl(oeffentlicheUrl: string | null | undefined, token: string): string {
  const basis = (oeffentlicheUrl ?? 'https://rechnungen.<domain>').replace(/\/+$/, '')
  return `claude mcp add --transport http biber-rechnungen ${basis}/mcp --header "Authorization: Bearer ${token}"`
}

/** Tools des MCP-Servers, wie im Design aufgezählt. */
export const MCP_TOOLS =
  'belege_suchen, beleg_abrufen, kosten_auswerten, korrespondent_abrufen, artikel_suchen, artikel_preisverlauf, beleg_hochladen'
