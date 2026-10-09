/** A Run as listed by GET /api/runs. Distance in metres, times in seconds, pace in seconds per km. */
export interface Run {
  id: number;
  name: string;
  sportType: string;
  /** Wall-clock start where the Run happened, without a time zone. */
  startLocal: string;
  distance: number;
  movingTime: number;
  pace: number | null;
}
