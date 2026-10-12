export const fullMonthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** Local midnight of a yyyy-MM-dd date (or the date part of a local date-time). */
export function localDate(text: string): Date {
  const [year, month, day] = text.slice(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Local midnight today. */
export function today(): Date {
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

/** Whole calendar days from <code>from</code> to <code>to</code>; negative when <code>to</code> is earlier. */
export function daysBetween(from: Date, to: Date): number {
  return Math.round((to.getTime() - from.getTime()) / 86_400_000);
}

/** Whole calendar days from today to a yyyy-MM-dd date. */
export function daysUntil(date: string): number {
  return daysBetween(today(), localDate(date));
}

/** "2026-10-31" for a local date. */
export function isoDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
