/**
 * Minimaler MCP-Client über Streamable HTTP (JSON-RPC 2.0): `initialize`, danach `tools/list` und `tools/call`.
 * Antworten kommen je nach Server als JSON oder als Server-Sent Events.
 */
export class McpClient {
  private sitzung: string | undefined
  private naechsteId = 1

  constructor(
    private readonly basisUrl: string,
    private readonly token: string,
  ) {}

  /** Roher Aufruf; liefert HTTP-Status und – falls vorhanden – das JSON-RPC-Ergebnis. */
  async aufrufen(methode: string, parameter: unknown = {}): Promise<{ status: number; ergebnis?: any; fehler?: any }> {
    const id = this.naechsteId++
    const antwort = await fetch(`${this.basisUrl}/mcp`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.token}`,
        'Content-Type': 'application/json',
        Accept: 'application/json, text/event-stream',
        ...(this.sitzung ? { 'Mcp-Session-Id': this.sitzung } : {}),
      },
      body: JSON.stringify({ jsonrpc: '2.0', id, method: methode, params: parameter }),
    })
    this.sitzung = antwort.headers.get('mcp-session-id') ?? this.sitzung
    if (!antwort.ok) return { status: antwort.status }
    const text = await antwort.text()
    const nachricht = (antwort.headers.get('content-type') ?? '').includes('text/event-stream')
      ? text
          .split('\n')
          .filter((z) => z.startsWith('data:'))
          .map((z) => JSON.parse(z.slice(5)))
          .find((n) => n.id === id)
      : JSON.parse(text)
    return { status: antwort.status, ergebnis: nachricht?.result, fehler: nachricht?.error }
  }

  async verbinden(): Promise<number> {
    const { status } = await this.aufrufen('initialize', {
      protocolVersion: '2025-06-18',
      capabilities: {},
      clientInfo: { name: 'biber-e2e', version: '1.0.0' },
    })
    if (status === 200) await this.benachrichtigen('notifications/initialized')
    return status
  }

  private async benachrichtigen(methode: string): Promise<void> {
    await fetch(`${this.basisUrl}/mcp`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${this.token}`,
        'Content-Type': 'application/json',
        Accept: 'application/json, text/event-stream',
        ...(this.sitzung ? { 'Mcp-Session-Id': this.sitzung } : {}),
      },
      body: JSON.stringify({ jsonrpc: '2.0', method: methode }),
    })
  }

  async werkzeuge(): Promise<string[]> {
    const { ergebnis } = await this.aufrufen('tools/list')
    return (ergebnis?.tools ?? []).map((t: { name: string }) => t.name)
  }

  /** Ruft ein Tool auf und liefert den Textinhalt (bei JSON-Text bereits geparst). */
  async werkzeugAufrufen(name: string, argumente: Record<string, unknown>): Promise<any> {
    const { status, ergebnis, fehler } = await this.aufrufen('tools/call', { name, arguments: argumente })
    if (status !== 200 || fehler) throw new Error(`tools/call ${name}: HTTP ${status} ${JSON.stringify(fehler)}`)
    if (ergebnis?.isError) throw new Error(`tools/call ${name} meldet Fehler: ${JSON.stringify(ergebnis.content)}`)
    if (ergebnis?.structuredContent) return ergebnis.structuredContent
    const text = (ergebnis?.content ?? []).map((c: { text?: string }) => c.text ?? '').join('')
    try {
      return JSON.parse(text)
    } catch {
      return text
    }
  }
}
