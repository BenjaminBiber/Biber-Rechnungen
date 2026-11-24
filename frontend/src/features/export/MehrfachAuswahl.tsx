import { useId } from 'react'
import { Icon } from '@/components/Icon'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

/** Filterfeld mit Mehrfachauswahl; ohne Auswahl zeigt es `leerText` („Alle“, „Beliebig“ …). */
export function MehrfachAuswahl({
  label,
  leerText,
  optionen,
  gewaehlt,
  onAendern,
}: {
  label: string
  leerText: string
  optionen: { id: string; name: string }[]
  gewaehlt: string[]
  onAendern: (ids: string[]) => void
}) {
  const id = useId()
  const namen = optionen.filter((o) => gewaehlt.includes(o.id)).map((o) => o.name)
  const text = namen.length === 0 ? leerText : namen.length <= 2 ? namen.join(', ') : `${namen.length} gewählt`

  function umschalten(optionId: string) {
    onAendern(gewaehlt.includes(optionId) ? gewaehlt.filter((g) => g !== optionId) : [...gewaehlt, optionId])
  }

  return (
    <div>
      <span className="label" id={`${id}-label`}>
        {label}
      </span>
      <Popover>
        <PopoverTrigger asChild>
          <button
            type="button"
            className="eingabe flex items-center justify-between text-left"
            aria-labelledby={`${id}-label ${id}-wert`}
          >
            <span id={`${id}-wert`} className="truncate">
              {text}
            </span>
            <Icon name="expand_more" className="text-gedaempft" />
          </button>
        </PopoverTrigger>
        <PopoverContent className="max-h-80 overflow-auto p-2">
          <fieldset className="m-0 border-0 p-0">
            <legend className="sr-only">{label}</legend>
            {optionen.map((o) => (
              <label
                key={o.id}
                className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-hover"
              >
                <input type="checkbox" checked={gewaehlt.includes(o.id)} onChange={() => umschalten(o.id)} />
                {o.name}
              </label>
            ))}
          </fieldset>
        </PopoverContent>
      </Popover>
    </div>
  )
}
