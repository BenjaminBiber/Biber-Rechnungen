/** Felder, die für die Baumdarstellung der Kategorien gebraucht werden. */
type BaumKategorie = { id: string; name: string; elternId?: string | null }

export type BaumEintrag<K extends BaumKategorie> = { kategorie: K; ebene: 0 | 1 }

const vergleich = (a: BaumKategorie, b: BaumKategorie) => a.name.localeCompare(b.name, 'de', { sensitivity: 'base' })

/** Hauptkategorien alphabetisch, jeweils gefolgt von ihren Unterkategorien (nur eine Ebene). */
export function kategorieBaum<K extends BaumKategorie>(kategorien: readonly K[]): BaumEintrag<K>[] {
  const ids = new Set(kategorien.map((k) => k.id))
  const istUnter = (k: K) => !!k.elternId && ids.has(k.elternId)
  const baum: BaumEintrag<K>[] = []
  for (const haupt of kategorien.filter((k) => !istUnter(k)).sort(vergleich)) {
    baum.push({ kategorie: haupt, ebene: 0 })
    for (const unter of kategorien.filter((k) => k.elternId === haupt.id).sort(vergleich))
      baum.push({ kategorie: unter, ebene: 1 })
  }
  return baum
}

/** „Kfz › Tanken“ für Unterkategorien, sonst der Name. */
export function vollerKategorieName(kategorie: BaumKategorie, kategorien: readonly BaumKategorie[]): string {
  const eltern = kategorie.elternId ? kategorien.find((k) => k.id === kategorie.elternId) : undefined
  return eltern ? `${eltern.name} › ${kategorie.name}` : kategorie.name
}
