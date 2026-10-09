/** The API speaks metres and seconds; these turn them into what the Athlete reads. */

export function formatDistance(metres: number): string {
  return `${(metres / 1000).toFixed(2)} km`;
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

function pad(value: number): string {
  return value.toString().padStart(2, '0');
}
