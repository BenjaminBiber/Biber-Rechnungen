import { useState, type FormEvent } from 'react'
import { useEinstellungen } from '@/api/einstellungen'
import { useTokenAnlegen, useTokens, useTokenWiderrufen } from '@/api/tokens'
import type { ApiToken, ApiTokenErstellt } from '@/api/typen'
import { Chip } from '@/components/Chip'
import { Icon } from '@/components/Icon'
import { useRolle } from '@/components/shell/useRolle'
import { useToast } from '@/components/toast'
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { formatDatum, formatDatumZeit } from '@/lib/format'
import { mcpBefehl, MCP_TOOLS } from './mcpBefehl'
import { TokenErstelltDialog } from './TokenErstelltDialog'

/** Bereich „API-Tokens (MCP)“ (Einstellungen.dc.html → `s.tokens`). */
export function ApiTokensBereich() {
  const { darfBearbeiten, rolle } = useRolle()
  const { data: tokens, isError, error } = useTokens()
  const { data: einstellungen } = useEinstellungen()
  const [erstellen, setErstellen] = useState(false)
  const [erstellt, setErstellt] = useState<ApiTokenErstellt>()
  const [widerrufen, setWiderrufen] = useState<ApiToken>()
  const url = einstellungen?.umgebung.oeffentlicheUrl

  return (
    <div className="flex flex-col gap-3.5">
      <div className="flex flex-wrap items-center justify-between gap-2.5">
        <h2 className="m-0 text-lg font-semibold text-text">API-Tokens (MCP)</h2>
        {/* Auch NurLesen darf einen Token erstellen, dann aber nur mit Scope „lesen“. */}
        <Button type="button" variant="primaer" onClick={() => setErstellen(true)}>
          <Icon name="add" />
          Token erstellen
        </Button>
      </div>
      <p className="gedaempft m-0 text-sm">
        Für Claude Code und Claude Desktop. Tokens werden nur als Hash gespeichert und einmalig angezeigt.
      </p>
      {tokens ? (
        <div className="overflow-x-auto">
          <table className="tabelle min-w-[620px]">
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Scopes</th>
                <th scope="col">Erstellt</th>
                <th scope="col">Zuletzt genutzt</th>
                <th scope="col">
                  <span className="sr-only">Aktion</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {tokens.map((t) => {
                const aus = !!t.widerrufenAm
                return (
                  <tr key={t.id} className={aus ? 'text-gedaempft' : undefined}>
                    <td>
                      <strong>{t.name}</strong>
                      <br />
                      <span className={`mono text-xs${aus ? '' : ' gedaempft'}`}>brk_…{t.endung}</span>
                    </td>
                    <td>
                      <span className="flex flex-wrap gap-1">
                        {t.scopes.map((s) => (
                          <Chip key={s} ton={aus ? 'grau' : 'blau'}>
                            {s}
                          </Chip>
                        ))}
                      </span>
                    </td>
                    <td>{formatDatum(t.erstelltAm)}</td>
                    <td>{t.zuletztGenutztAm ? formatDatumZeit(t.zuletztGenutztAm) : '—'}</td>
                    <td>
                      {aus ? (
                        <Chip ton="grau">widerrufen</Chip>
                      ) : (
                        darfBearbeiten && (
                          <Button
                            type="button"
                            size="klein"
                            className="text-rot-text"
                            aria-label={`${t.name} widerrufen`}
                            onClick={() => setWiderrufen(t)}
                          >
                            Widerrufen
                          </Button>
                        )
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      ) : isError ? (
        <p role="alert" className="m-0 text-sm text-gefahr">
          {error.message}
        </p>
      ) : (
        <p className="gedaempft m-0 text-sm">Lädt …</p>
      )}
      <h3 className="m-0 text-base font-semibold text-text">Verbinden</h3>
      <pre aria-label="Befehl zum Verbinden" className="mono m-0 rounded-lg bg-flaeche p-3 text-xs whitespace-pre-wrap">
        {mcpBefehl(url, '<API-Token>')}
      </pre>
      <span className="gedaempft text-[13px]">Tools: {MCP_TOOLS}</span>

      {erstellen && (
        <TokenErstellenDialog
          nurLesen={rolle === 'NurLesen'}
          onErstellt={(e) => {
            setErstellen(false)
            setErstellt(e)
          }}
          onSchliessen={() => setErstellen(false)}
        />
      )}
      {erstellt && (
        <TokenErstelltDialog erstellt={erstellt} oeffentlicheUrl={url} onSchliessen={() => setErstellt(undefined)} />
      )}
      {widerrufen && <WiderrufenDialog token={widerrufen} onSchliessen={() => setWiderrufen(undefined)} />}
    </div>
  )
}

/** Dialog „Token erstellen“: Name und Scopes; „lesen“ ist immer gewählt. */
function TokenErstellenDialog({
  nurLesen,
  onErstellt,
  onSchliessen,
}: {
  nurLesen: boolean
  onErstellt: (e: ApiTokenErstellt) => void
  onSchliessen: () => void
}) {
  const toast = useToast()
  const anlegen = useTokenAnlegen()
  const [name, setName] = useState('')
  const [upload, setUpload] = useState(false)

  const absenden = (ev: FormEvent) => {
    ev.preventDefault()
    anlegen
      .mutateAsync({ name: name.trim(), scopes: upload && !nurLesen ? ['lesen', 'upload'] : ['lesen'] })
      .then(onErstellt, (f: Error) => void toast.fehler(f.message))
  }

  return (
    <Dialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <DialogContent>
        <form aria-label="Token erstellen" className="flex flex-col gap-4" onSubmit={absenden}>
          <DialogHeader>
            <DialogTitle>Token erstellen</DialogTitle>
          </DialogHeader>
          <div>
            <label className="label" htmlFor="token-name">
              Name
            </label>
            <input
              id="token-name"
              className="eingabe"
              autoComplete="off"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>
          <fieldset className="m-0 flex flex-col gap-2 border-0 p-0">
            <legend className="label">Scopes</legend>
            <span className="flex items-center gap-2 text-sm">
              <input type="checkbox" id="scope-lesen" className="accent-akzent" checked readOnly disabled />
              <label htmlFor="scope-lesen">lesen</label>
            </span>
            <span className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                id="scope-upload"
                className="accent-akzent"
                checked={upload && !nurLesen}
                disabled={nurLesen}
                onChange={(e) => setUpload(e.target.checked)}
              />
              <label htmlFor="scope-upload">upload</label>
            </span>
          </fieldset>
          <DialogFooter>
            <Button type="button" onClick={onSchliessen}>
              Abbrechen
            </Button>
            <Button type="submit" variant="primaer" disabled={!name.trim() || anlegen.isPending}>
              Erstellen
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Rückfrage vor dem Widerrufen. */
function WiderrufenDialog({ token, onSchliessen }: { token: ApiToken; onSchliessen: () => void }) {
  const toast = useToast()
  const widerrufen = useTokenWiderrufen()
  const bestaetigen = () =>
    widerrufen.mutateAsync(token.id).then(
      () => {
        toast.erfolg('Token widerrufen')
        onSchliessen()
      },
      (f: Error) => void toast.fehler(f.message),
    )

  return (
    <AlertDialog open onOpenChange={(offen) => !offen && onSchliessen()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Token „{token.name}“ widerrufen?</AlertDialogTitle>
        </AlertDialogHeader>
        <AlertDialogDescription>
          Anwendungen mit diesem Token verlieren sofort den Zugriff. Das lässt sich nicht rückgängig machen.
        </AlertDialogDescription>
        <AlertDialogFooter>
          <Button type="button" onClick={onSchliessen}>
            Abbrechen
          </Button>
          <Button
            type="button"
            variant="gefaehrlich"
            disabled={widerrufen.isPending}
            onClick={() => void bestaetigen()}
          >
            Widerrufen
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
