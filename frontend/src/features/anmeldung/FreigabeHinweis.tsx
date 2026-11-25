import { useNavigate } from '@tanstack/react-router'
import { Icon } from '@/components/Icon'
import { Button } from '@/components/ui/button'
import { grosserKnopf } from './gemeinsam'

/** Schritt „freigabe“: Konto ist angelegt, ein Admin muss es freigeben. */
export function FreigabeHinweis({ email }: { email?: string }) {
  const navigate = useNavigate()
  return (
    <div className="flex flex-col items-center gap-4 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-orange-flaeche text-orange-text">
        <Icon name="hourglass_top" className="text-[32px] [vertical-align:0]" />
      </div>
      <h1 className="m-0 text-[22px] font-semibold">Konto wartet auf Freigabe</h1>
      <p className="m-0 text-sm leading-normal text-gedaempft-stark">
        Dein Konto {email && <strong>{email}</strong>}
        {email ? ' ' : ''}wurde angelegt. Ein Admin wurde benachrichtigt und muss es freigeben, bevor du dich anmelden
        kannst.
      </p>
      <Button className={grosserKnopf} onClick={() => void navigate({ to: '/anmelden', search: {} })}>
        Zur Anmeldung
      </Button>
    </div>
  )
}
