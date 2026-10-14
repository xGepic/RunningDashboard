export const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

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

/** "2026-10" for today's month, in local time. */
export function currentMonth(): string {
  return monthKey(new Date());
}

/** "2026-10" for a local date's month. */
export function monthKey(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
}

/** The first day of the month `offset` months after `month` ("2026-10"). */
export function monthDate(month: string, offset: number): Date {
  const [year, monthNumber] = month.split('-').map(Number);
  return new Date(year, monthNumber - 1 + offset, 1);
}

/** The last day of the month starting on `monthStart`. */
export function lastDayOf(monthStart: Date): Date {
  return new Date(monthStart.getFullYear(), monthStart.getMonth() + 1, 0);
}

/** 0 for Monday to 6 for Sunday: days since the Training Week started. */
export function daysSinceMonday(date: Date): number {
  return (date.getDay() + 6) % 7;
}

/** The local date `days` days after `date`; earlier for a negative number. */
export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

/** The Monday starting the Training Week that `date` falls in. */
export function mondayOf(date: Date): Date {
  return addDays(date, -daysSinceMonday(date));
}
