import { monthNames } from '../shared/dates';

/** A month as the Calendar's URL fragment names it: "Jan" to "Dec", e.g. /calendar/2025#Dec. */
export const monthFragments = monthNames;

/** /calendar for the current year, /calendar/2025 for another. */
export function yearPath(year: number): string[] {
  return year === new Date().getFullYear() ? ['/calendar'] : ['/calendar', String(year)];
}
