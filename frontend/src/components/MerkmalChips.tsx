import type { Merkmal } from '@/api/typen'

/** Merkmale als graue Chips („Größe 42“), der Name gedämpft; nichts ohne Merkmale. */
export function MerkmalChips({ merkmale, label = 'Merkmale' }: { merkmale: readonly Merkmal[]; label?: string }) {
  if (merkmale.length === 0) return null
  return (
    <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0" aria-label={label}>
      {merkmale.map((m, i) => (
        <li key={i} className="chip chip-grau">
          {m.name && (
            <>
              <span className="font-normal opacity-75">{m.name}</span>{' '}
            </>
          )}
          {m.wert}
        </li>
      ))}
    </ul>
  )
}
