/// <reference types="node" />
import { execFileSync } from 'node:child_process'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { afterAll, describe, expect, it } from 'vitest'

const frontend = resolve(import.meta.dirname, '../..')
const ordner = mkdtempSync(join(tmpdir(), 'biber-schema-'))

afterAll(() => rmSync(ordner, { recursive: true, force: true }))

describe('schema.d.ts', () => {
  it('entspricht dem Generat aus docs/api/openapi.json (npm run gen:api)', () => {
    const ziel = join(ordner, 'schema.d.ts')
    execFileSync(
      process.execPath,
      [join(frontend, 'node_modules/openapi-typescript/bin/cli.js'), '../docs/api/openapi.json', '-o', ziel],
      { cwd: frontend, stdio: 'pipe' },
    )
    const ohneCr = (text: string) => text.replace(/\r\n/g, '\n')
    const eingecheckt = ohneCr(readFileSync(join(frontend, 'src/api/schema.d.ts'), 'utf8'))
    expect(eingecheckt).toBe(ohneCr(readFileSync(ziel, 'utf8')))
  }, 30_000)
})
