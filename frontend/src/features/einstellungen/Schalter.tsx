/** Ein/Aus-Schalter (`role="switch"`) wie `.sw` in Einstellungen.dc.html. */
export function Schalter({
  an,
  onAendern,
  label,
  disabled,
}: {
  an: boolean
  onAendern: (an: boolean) => void
  label: string
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={an}
      aria-label={label}
      disabled={disabled}
      onClick={() => onAendern(!an)}
      className={`relative inline-block h-6 w-10 flex-none cursor-pointer rounded-xl border-0 p-0 disabled:cursor-not-allowed disabled:opacity-60 ${
        an ? 'bg-akzent' : 'bg-schalter-aus'
      }`}
    >
      <span
        aria-hidden="true"
        className={`absolute top-[3px] h-[18px] w-[18px] rounded-[9px] bg-white shadow-[0_1px_2px_rgba(0,0,0,0.2)] ${
          an ? 'left-[19px]' : 'left-[3px]'
        }`}
      />
    </button>
  )
}
