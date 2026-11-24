import type { BelegListeneintrag } from '@/api/typen'
import { Icon } from '@/components/Icon'

/** ⋮-Knopf je Beleg. Das Menü folgt in einem eigenen Ticket. */
export function Zeilenmenue({ beleg }: { beleg: BelegListeneintrag }) {
  return (
    <button
      type="button"
      aria-label={`Aktionen für ${beleg.titel}`}
      className="btn btn-klein w-[30px] border-transparent px-0 text-gedaempft"
    >
      <Icon name="more_vert" />
    </button>
  )
}
