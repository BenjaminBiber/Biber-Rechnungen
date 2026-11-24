import nodemailer from 'nodemailer'

export const POSTFACH = 'rechnungen@e2e.local'
const GREENMAIL_API = 'http://localhost:8090'

const transport = nodemailer.createTransport({ host: 'localhost', port: 3025, secure: false, ignoreTLS: true })

export interface Anhang {
  dateiname: string
  inhalt: Buffer
  typ?: string
}

/** Schickt eine Mail per SMTP an das GreenMail-Postfach, das die API per IMAP abruft. */
export async function mailSenden(betreff: string, anhaenge: Anhang[] = []): Promise<void> {
  await transport.sendMail({
    from: 'lieferant@e2e.local',
    to: POSTFACH,
    subject: betreff,
    text: `Testmail: ${betreff}`,
    attachments: anhaenge.map((a) => ({ filename: a.dateiname, content: a.inhalt, contentType: a.typ })),
  })
}

/** Betreffzeilen der Mails in einem IMAP-Ordner des Postfachs (über die REST-API von GreenMail). */
export async function mailsImOrdner(ordner: string): Promise<string[]> {
  const antwort = await fetch(`${GREENMAIL_API}/api/user/${encodeURIComponent(POSTFACH)}/messages/${encodeURIComponent(ordner)}/`)
  if (!antwort.ok) return []
  const mails = (await antwort.json()) as { subject: string }[]
  return mails.map((m) => m.subject)
}
