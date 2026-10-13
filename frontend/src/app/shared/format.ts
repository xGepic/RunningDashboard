/** The API speaks metres and seconds; these turn them into what the Athlete reads. */

/** Kilometres, or miles for an Athlete who reads distances in them. */
export type DistanceUnit = 'km' | 'mi';

const metresPer: Record<DistanceUnit, number> = { km: 1000, mi: 1609.344 };

/** Metres in kilometres or miles, unrounded. */
export function distanceIn(metres: number, unit: DistanceUnit): number {
  return metres / metresPer[unit];
}

export function formatDistance(metres: number, unit: DistanceUnit = 'km', fractionDigits = 2): string {
  return `${(metres / metresPer[unit]).toFixed(fractionDigits)} ${unit}`;
}

const wholeNumber = new Intl.NumberFormat('en', { maximumFractionDigits: 0 });

/** Totals: whole kilometres, e.g. "4,321 km". */
export function formatTotalDistance(metres: number): string {
  return `${wholeNumber.format(metres / 1000)} km`;
}

/** Hours and minutes, e.g. "7h 17m" or "1h 06m". */
export function formatHoursMinutes(seconds: number): string {
  return `${Math.floor(seconds / 3600)}h ${String(Math.floor((seconds % 3600) / 60)).padStart(2, '0')}m`;
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

/** Pace as min:ss /km, or per mile as min:ss /mi; a Run without distance has none. */
export function formatPace(secondsPerKm: number | null, unit: DistanceUnit = 'km'): string {
  if (secondsPerKm === null) return '–';
  const total = Math.round((secondsPerKm * metresPer[unit]) / 1000);
  return `${Math.floor(total / 60)}:${pad(total % 60)} /${unit}`;
}

/** Avg Speed as km/h, or as mph for miles, with one decimal; a Ride without Moving Time has none. */
export function formatSpeed(kilometresPerHour: number | null, unit: DistanceUnit = 'km'): string {
  if (kilometresPerHour === null) return '–';
  const speed = (kilometresPerHour * 1000) / metresPer[unit];
  return `${speed.toFixed(1)} ${unit === 'km' ? 'km/h' : 'mph'}`;
}

/** Whole seconds per km against the Target Pace: "3 s/km slower", "1:12 /km faster" or "on Target Pace". */
export function formatPaceDifference(secondsPerKm: number): string {
  const seconds = Math.round(Math.abs(secondsPerKm));
  if (seconds === 0) return 'on Target Pace';
  const amount = seconds < 60 ? `${seconds} s/km` : formatPace(seconds);
  return `${amount} ${secondsPerKm > 0 ? 'slower' : 'faster'}`;
}

/** A result against its Target Time: "0:11 over", "4:40 under" or "on target". */
export function formatDifference(seconds: number): string {
  if (seconds === 0) return 'on target';
  return `${formatDuration(Math.abs(seconds))} ${seconds > 0 ? 'over' : 'under'}`;
}

/** Seconds from "h:mm:ss" or "m:ss" as the Athlete types it; null if it is not one. */
export function parseDuration(text: string): number | null {
  const match = /^(?:(\d+):)?(\d{1,2}):(\d{2})$/.exec(text.trim());
  if (!match) return null;
  const [hours, minutes, seconds] = [match[1] ?? '0', match[2], match[3]].map(Number);
  if (seconds >= 60 || (match[1] !== undefined && minutes >= 60)) return null;
  return hours * 3600 + minutes * 60 + seconds;
}

/** Metres from kilometres as typed, accepting a decimal comma; null if it is not a positive number. */
export function kilometresToMetres(text: string): number | null {
  const km = Number(text.trim().replace(',', '.'));
  return text.trim() === '' || Number.isNaN(km) || km <= 0 ? null : Math.round(km * 1000 * 10) / 10;
}

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
