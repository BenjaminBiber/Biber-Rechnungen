import type { Page } from '@playwright/test'

/**
 * Öffnet einen Tab des Beleg-Details. Was nicht in die Tab-Leiste passt, steht im Menü „Mehr“
 * (siehe TabLeiste.tsx) – je nach Breite also direkt in der Leiste oder dahinter.
 */
export async function belegTabOeffnen(page: Page, name: string | RegExp): Promise<void> {
  const leiste = page.getByRole('navigation', { name: 'Belegdaten' })
  const direkt = leiste.getByRole('link', { name })
  if (await direkt.isVisible()) {
    await direkt.click()
    return
  }
  await leiste.getByRole('button', { name: /^Weitere Tabs/ }).click()
  await page.getByRole('menuitem', { name }).click()
}
