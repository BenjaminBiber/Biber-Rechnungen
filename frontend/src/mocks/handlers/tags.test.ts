import { describe, expect, it } from 'vitest'
import type { Tag } from '@/api/typen'
import { tagIds } from '../fixtures/ids'

const basis = `${location.origin}/api`

const sende = (methode: 'POST' | 'PUT', pfad: string, koerper: unknown) =>
  fetch(`${basis}${pfad}`, {
    method: methode,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(koerper),
  })

async function holeTags(): Promise<Tag[]> {
  const antwort = await fetch(`${basis}/tags`)
  expect(antwort.ok).toBe(true)
  return (await antwort.json()) as Tag[]
}

describe('Tags-Handler', () => {
  it('POST mit istInbox=true entzieht dem bisherigen Inbox-Tag die Sonderform', async () => {
    expect((await holeTags()).filter((t) => t.istInbox).map((t) => t.id)).toEqual([tagIds.inbox])

    const antwort = await sende('POST', '/tags', { name: 'Neuer Eingang', istInbox: true })
    expect(antwort.status).toBe(201)
    const neu = (await antwort.json()) as Tag

    const inbox = (await holeTags()).filter((t) => t.istInbox)
    expect(inbox.map((t) => t.id)).toEqual([neu.id])
  })

  it('POST ohne istInbox lässt den vorhandenen Inbox-Tag unberührt', async () => {
    await sende('POST', '/tags', { name: 'Gewöhnlich', istInbox: false })
    expect((await holeTags()).filter((t) => t.istInbox).map((t) => t.id)).toEqual([tagIds.inbox])
  })

  it('PUT mit istInbox=true übernimmt die Sonderform ebenfalls exklusiv', async () => {
    const antwort = await sende('POST', '/tags', { name: 'Später', istInbox: false })
    const tag = (await antwort.json()) as Tag
    const geaendert = await sende('PUT', `/tags/${tag.id}`, { name: 'Später', istInbox: true })
    expect(geaendert.ok).toBe(true)
    expect((await holeTags()).filter((t) => t.istInbox).map((t) => t.id)).toEqual([tag.id])
  })
})
