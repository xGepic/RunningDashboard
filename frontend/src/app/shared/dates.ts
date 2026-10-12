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

/** Local midnight <code>days</code> calendar days after <code>date</code>; earlier for a negative number. */
export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

/** "2026-10-31", in local time: a date as the API writes it. */
export function isoDate(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Whole calendar days from <code>from</code> to <code>to</code>; negative when <code>to</code> is earlier. */
export function daysBetween(from: Date, to: Date): number {
  return Math.round((to.getTime() - from.getTime()) / 86_400_000);
}

/** Whole calendar days from today to a yyyy-MM-dd date. */
export function daysUntil(date: string): number {
  return daysBetween(today(), localDate(date));
}
