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
