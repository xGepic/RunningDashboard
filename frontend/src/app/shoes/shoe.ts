/** A Shoe as listed by GET /api/shoes: active ones first, then retired, each most recently used first. */
export interface Shoe {
  /** Strava's gear id. */
  id: string;
  name: string;
  /** Metres, as Strava counts them. */
  distance: number;
  retired: boolean;
  runs: number;
  /** Local start of the latest Run in this Shoe. */
  lastRun: string | null;
}
