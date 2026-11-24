import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Fügt Klassen zusammen und löst Tailwind-Konflikte auf (shadcn-Konvention). */
export function cn(...klassen: ClassValue[]): string {
  return twMerge(clsx(klassen))
}
