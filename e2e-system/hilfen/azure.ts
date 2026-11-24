const WIREMOCK = 'http://localhost:8089'

/** Anzahl der Analyse-Aufrufe, die azure-mock bisher erhalten hat (WireMock-Journal). */
export async function analyseAufrufe(): Promise<number> {
  const antwort = await fetch(`${WIREMOCK}/__admin/requests/count`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ method: 'POST', urlPath: '/documentintelligence/documentModels/prebuilt-invoice:analyze' }),
  })
  if (!antwort.ok) throw new Error(`WireMock-Journal nicht lesbar: HTTP ${antwort.status}`)
  return ((await antwort.json()) as { count: number }).count
}
