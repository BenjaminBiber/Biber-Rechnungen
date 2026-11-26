import { copyFile, mkdir } from 'node:fs/promises'
const assets = new URL('./assets/', import.meta.url)
await mkdir(assets, { recursive: true })
for (const name of ['suchen', 'reparieren', 'warten']) {
  await copyFile(new URL(`../../../frontend/src/assets/fehler/${name}.webp`, import.meta.url), new URL(`${name}.webp`, assets))
}
