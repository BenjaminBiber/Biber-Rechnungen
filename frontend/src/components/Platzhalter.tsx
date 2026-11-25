/** Vorläufiger Seiteninhalt, bis das View-Ticket die Route füllt. */
export function Platzhalter({ titel }: { titel: string }) {
  return (
    <main className="seite">
      <h1 className="m-0 text-[22px] font-semibold text-text">{titel}</h1>
      <p className="gedaempft mt-2">Diese Seite wird noch umgesetzt.</p>
    </main>
  )
}
