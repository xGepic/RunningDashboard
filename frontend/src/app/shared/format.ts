/** The API speaks metres and seconds; these turn them into what the Athlete reads. */

export function formatDistance(metres: number): string {
  return `${(metres / 1000).toFixed(2)} km`;
}

const wholeNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** Totals: whole kilometres, e.g. "4,321 km". */
export function formatTotalDistance(metres: number): string {
  return `${wholeNumber.format(metres / 1000)} km`;
}

/** Totals: whole hours, e.g. "512 h". */
export function formatHours(seconds: number): string {
  return `${wholeNumber.format(seconds / 3600)} h`;
}

/** h:mm:ss from one hour on, otherwise m:ss. */
export function formatDuration(seconds: number): string {
  const total = Math.round(seconds);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = total % 60;
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(secs)}` : `${minutes}:${pad(secs)}`;
}

/** Pace as min:ss /km; a Run without distance has none. */
export function formatPace(secondsPerKm: number | null): string {
  if (secondsPerKm === null) return '–';
  const total = Math.round(secondsPerKm);
  return `${Math.floor(total / 60)}:${pad(total % 60)} /km`;
}

/** Seconds from "h:mm:ss" or "m:ss" as the Athlete types it; null if it is not one. */
export function parseDuration(text: string): number | null {
  const match = /^(?:(\d+):)?(\d{1,2}):(\d{2})$/.exec(text.trim());
  if (!match) return null;
  const [hours, minutes, seconds] = [match[1] ?? '0', match[2], match[3]].map(Number);
  if (seconds >= 60 || (match[1] !== undefined && minutes >= 60)) return null;
  return hours * 3600 + minutes * 60 + seconds;
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
