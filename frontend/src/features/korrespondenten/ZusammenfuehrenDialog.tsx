import { useState } from 'react'
import { useKorrespondent, useKorrespondentenZusammenfuehren } from '@/api/korrespondenten'
import type { KorrespondentListeneintrag } from '@/api/typen'
import { useToast } from '@/components/toast'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'

/** Kennzeile: USt-IdNr., Anzahl IBANs (aus dem Detail), Logo. */
function Kennzeile({ k }: { k: KorrespondentListeneintrag }) {
  const { data } = useKorrespondent(k.id)
  const teile = [
    k.ustIdNr ?? 'keine USt-IdNr.',
    data && `${data.bankverbindungen.length} ${data.bankverbindungen.length === 1 ? 'IBAN' : 'IBANs'}`,
    k.logo.quelle !== 'Keine' && 'Logo',
  ].filter(Boolean)
  return <span className="gedaempft block text-xs">{teile.join(' · ')}</span>
}

function belegeText(anzahl: number) {
  return `${anzahl} ${anzahl === 1 ? 'Beleg' : 'Belege'}`
}

/** Dialog „n Korrespondenten zusammenführen“ mit Wahl des behaltenen Korrespondenten. */
export function ZusammenfuehrenDialog({
  korrespondenten,
  onSchliessen,
  onZusammengefuehrt,
}: {
  korrespondenten: KorrespondentListeneintrag[]
  onSchliessen: () => void
  onZusammengefuehrt: () => void
}) {
  const toast = useToast()
  const zusammenfuehren = useKorrespondentenZusammenfuehren()
  const groesster = korrespondenten.reduce<KorrespondentListeneintrag | undefined>(
    (max, k) => (!max || k.anzahlBelege > max.anzahlBelege ? k : max),
    undefined,
  )
  const [behaltenId, setBehaltenId] = useState(groesster?.id ?? '')
  const quellen = korrespondenten.filter((k) => k.id !== behaltenId)
  const behalten = korrespondenten.find((k) => k.id === behaltenId)
  const umzuhaengen = quellen.reduce((s, k) => s + k.anzahlBelege, 0)

  // `mutateAsync` statt Callbacks an `mutate`: Nach dem Neuladen fehlen die Quellen in der Liste und der
  // Dialog ist schon abgebaut – Callbacks eines abgebauten Beobachters liefen nicht mehr.
  const absenden = () =>
    zusammenfuehren.mutateAsync({ behaltenId, quellIds: quellen.map((k) => k.id) }).then(
      () => {
        toast.erfolg('Korrespondenten zusammengeführt')
        onZusammengefuehrt()
      },
      (fehler: Error) => void toast.fehler(fehler.message),
    )

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{korrespondenten.length} Korrespondenten zusammenführen</DialogTitle>
        </DialogHeader>
        <div>
          <p className="label mb-2">Behalten wird</p>
          <RadioGroup value={behaltenId} onValueChange={setBehaltenId} aria-label="Behalten wird">
            {korrespondenten.map((k) => (
              <div key={k.id} className="flex items-start gap-3 rounded-[8px] border border-rand p-3">
                <RadioGroupItem id={`behalten-${k.id}`} value={k.id} aria-label={k.name} className="mt-0.5" />
                <span className="min-w-0 flex-1">
                  <span className="flex justify-between gap-2">
                    <label htmlFor={`behalten-${k.id}`} className="cursor-pointer font-medium">
                      {k.name}
                    </label>
                    <span className="gedaempft text-sm">{belegeText(k.anzahlBelege)}</span>
                  </span>
                  <Kennzeile k={k} />
                </span>
              </div>
            ))}
          </RadioGroup>
        </div>
        {behalten && (
          <p className="m-0 rounded-[8px] bg-akzent-zart p-3 text-sm">
            Die {belegeText(umzuhaengen)} werden umgehängt, {quellen.map((k) => `„${k.name}“`).join(', ')} wird als
            Alias übernommen. Ablagepfade der Dateien bleiben, wo sie sind.
          </p>
        )}
        <DialogFooter>
          <Button onClick={onSchliessen}>Abbrechen</Button>
          <Button
            variant="primaer"
            disabled={!behalten || quellen.length === 0 || zusammenfuehren.isPending}
            laedt={zusammenfuehren.isPending}
            onClick={absenden}
          >
            Zusammenführen
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
